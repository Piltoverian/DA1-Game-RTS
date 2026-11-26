using System;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEditor;
using UnityEngine;

// Standalone batch entry: -executeMethod RejoinNetworkingChecks.Run.
public static class RejoinNetworkingChecks
{
    static int checks;
    [MenuItem("Tools/RTS/Check Rejoin Networking")]
    public static void Run()
    {
        checks = 0;
        CheckTickets(); CheckPersistentJoin(); CheckDeadline(); CheckManifest(); CheckTicketPersistence(); CheckOwnedClientDisconnect(); CheckSessionCanvasLifetime(); CheckRetryRequiresReceipt();
        Debug.Log($"Rejoin networking checks passed: {checks} assertions.");
    }

    static void Assert(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(message);
    }

    static void CheckTickets()
    {
        var first = PlayerResumeTicket.Create(); var second = PlayerResumeTicket.Create();
        Assert(first.Length == 44 && Convert.FromBase64String(first.ToString()).Length == 32, "Resume secrets must contain 256 random bits");
        Assert(!first.Equals(second), "Separately issued secrets must differ");
        Assert(PlayerResumeTicket.TryHash(first, out var hash) && !hash.Equals(first), "Server storage must contain a digest, not the credential");
        Assert(PlayerResumeTicket.Matches(first, hash) && !PlayerResumeTicket.Matches(second, hash), "Only the matching secret authenticates");
        Assert(!PlayerResumeTicket.Matches(default, hash) && !PlayerResumeTicket.TryHash(new FixedString128Bytes("not-a-ticket"), out _), "Empty/malformed credentials must fail closed");
    }

    static void CheckPersistentJoin()
    {
        using var world = new World("Persistent rejoin checks", WorldFlags.GameServer);
        var em = world.EntityManager;
        var match = em.CreateEntity(typeof(MatchSession)); em.SetComponentData(match, new MatchSession { Id = 100 });
        var source = em.CreateEntity(); em.AddComponentObject(source, new TerrainVisualSource { Epoch = 7 });
        var context = em.CreateEntity(typeof(PlayerContext), typeof(ResourcePair));
        em.SetComponentData(context, new PlayerContext { PlayerId = 2 });
        em.GetBuffer<ResourcePair>(context).Add(new ResourcePair(ResourceType.Gold, 150));
        var unit = em.CreateEntity(typeof(EntityOwner)); em.SetComponentData(unit, new EntityOwner { PlayerID = 2 });
        var root = em.CreateEntity(typeof(PlayerBootstrapState), typeof(PlayerBootstrapSlot));
        em.SetComponentData(root, new PlayerBootstrapState { Phase = PlayerBootstrapPhase.Ready });
        em.GetBuffer<PlayerBootstrapSlot>(root).Add(new PlayerBootstrapSlot { PlayerId = 2, Context = context });
        var join = world.GetOrCreateSystemManaged<PlayerJoinServerSystem>();
        var lifecycle = world.GetOrCreateSystemManaged<PlayerSessionLifecycleSystem>();
        var first = Connection(17); Submit(first, default); join.Update();
        Assert(em.HasComponent<PlayerConnection>(first), "An initial join must claim an unused slot");
        Assert(PlayerSessionSlots.TryGetSlot(em, 2, out var slot) && slot.Claimed && slot.JoinGeneration == 1, "Claim state belongs to the persistent roster");
        FixedString128Bytes secret = default;
        using (var tickets = em.CreateEntityQuery(typeof(PlayerResumeTicketRpc), typeof(SendRpcCommandRequest)))
        using (var entities = tickets.ToEntityArray(Allocator.Temp))
            foreach (var entity in entities)
                if (em.GetComponentData<SendRpcCommandRequest>(entity).TargetConnection == first) secret = em.GetComponentData<PlayerResumeTicketRpc>(entity).Ticket;
        Assert(PlayerResumeTicket.Matches(secret, slot.TicketHash), "The private ticket must authenticate its server digest");
        em.DestroyEntity(first); lifecycle.Update();
        Assert(PlayerSessionSlots.TryGetSlot(em, 2, out slot) && slot.Claimed && slot.State == PlayerSlotState.Disconnected, "Transport destruction must retain the claim");
        Assert(em.Exists(context) && em.Exists(unit) && em.GetBuffer<ResourcePair>(context)[0].Amount == 150
            && em.GetComponentData<EntityOwner>(unit).PlayerID == 2, "Disconnect must preserve context, resources and gameplay ownership");
        var stranger = Connection(18); Submit(stranger, default); join.Update();
        Assert(!em.HasComponent<PlayerConnection>(stranger) && em.HasComponent<PlayerJoinDenied>(stranger), "A fresh connection cannot steal a disconnected claimed slot");
        var invalid = Connection(19); Submit(invalid, new PlayerJoinRequest { ResumeMatchSessionId = 100, ResumeTicket = PlayerResumeTicket.Create() }); join.Update();
        Assert(!em.HasComponent<PlayerConnection>(invalid), "Session/player identifiers cannot authenticate a resume");
        var resumed = Connection(20); Submit(resumed, new PlayerJoinRequest { ResumeMatchSessionId = 100, ResumeTicket = secret }); join.Update();
        var player = em.GetComponentData<PlayerConnection>(resumed);
        Assert(player.PlayerId == 2 && player.JoinGeneration == 2 && player.MatchSessionId == 100, "A valid secret must rebind the same slot with a new generation");
        em.AddComponentData(resumed, new PlayerReady { MatchSessionId = 100, JoinGeneration = 2, Epoch = 7 });
        var replacement = Connection(21); Submit(replacement, new PlayerJoinRequest { ResumeMatchSessionId = 100, ResumeTicket = secret }); join.Update();
        Assert(em.HasComponent<NetworkStreamRequestDisconnect>(resumed) && !em.HasComponent<PlayerReady>(resumed), "A valid takeover must immediately invalidate the old ready connection");
        Assert(!PlayerSessionSlots.IsCurrent(em, resumed, player) && PlayerSessionSlots.TryGetSlot(em, 2, out slot)
            && slot.Connection == replacement && slot.JoinGeneration == 3, "Old connections/generations must lose authority");
        Assert(em.Exists(match) && em.Exists(context) && em.Exists(unit) && em.GetBuffer<ResourcePair>(context)[0].Amount == 150,
            "Resume must retain the existing match and simulation state");
        var wrongMatch = Connection(22); Submit(wrongMatch, new PlayerJoinRequest { ResumeMatchSessionId = 101, ResumeTicket = secret }); join.Update();
        Assert(!em.HasComponent<PlayerConnection>(wrongMatch), "A ticket from another match must be rejected");

        Entity Connection(int id)
        { var entity = em.CreateEntity(typeof(NetworkId)); em.SetComponentData(entity, new NetworkId { Value = id }); return entity; }
        void Submit(Entity connection, PlayerJoinRequest value)
        {
            var request = em.CreateEntity(typeof(PlayerJoinRequest), typeof(ReceiveRpcCommandRequest));
            em.SetComponentData(request, value); em.SetComponentData(request, new ReceiveRpcCommandRequest { SourceConnection = connection });
        }
    }

    static void CheckDeadline()
    {
        using var world = new World("Player deadline checks", WorldFlags.GameServer);
        var em = world.EntityManager;
        var root = em.CreateEntity(typeof(PlayerBootstrapSlot));
        var roster = em.GetBuffer<PlayerBootstrapSlot>(root);
        roster.Add(new PlayerBootstrapSlot { PlayerId = 1 }); roster.Add(new PlayerBootstrapSlot { PlayerId = 2 }); roster.Add(new PlayerBootstrapSlot { PlayerId = 3 });
        PlayerSessionSlots.Ensure(em, 100, out _);
        var ready = Assign(0, true); var loading = Assign(1, false);
        Assert(!PlayerSessionSlots.AdvanceWaiting(em, root, 10, 7), "Waiting must permit initial loading");
        Assert(em.GetComponentData<PlayerJoinTimeout>(root).Deadline == 40, "The default deadline must be 30 seconds from WaitingForPlayers");
        Assert(!PlayerSessionSlots.AdvanceWaiting(em, root, 39.99, 7), "Missing players must not start before the deadline");
        Assert(PlayerSessionSlots.AdvanceWaiting(em, root, 40, 7), "Deadline must start with remaining ready players");
        Assert(!em.HasComponent<NetworkStreamRequestDisconnect>(ready) && !em.HasComponent<NetworkStreamRequestDisconnect>(loading), "Roster deadline must preserve admitted transports and their independent loading windows");
        var slots = em.GetBuffer<PersistentPlayerSlot>(root, true);
        Assert(slots.Length == 3 && slots[1].Claimed && slots[1].State == PlayerSlotState.Loading
            && !slots[2].Claimed && slots[2].State == PlayerSlotState.Disconnected, "Deadline must retain both claimed and never-claimed slots");
        em.SetComponentData(root, new PlayerJoinTimeout { Seconds = 5 });
        foreach (var connection in new[] { ready, loading }) em.DestroyEntity(connection);
        Assert(!PlayerSessionSlots.AdvanceWaiting(em, root, 100, 7)
            && PlayerSessionSlots.AdvanceWaiting(em, root, 105, 7), "Configured deadline must allow starting with no connected players");
        em.SetComponentData(root, new PlayerJoinTimeout { Seconds = 30 });
        Assign(0, true); Assign(1, true); Assign(2, true);
        Assert(PlayerSessionSlots.AdvanceWaiting(em, root, 200, 7), "Every ready slot must start immediately");

        Entity Assign(int index, bool isReady)
        {
            var slot = em.GetBuffer<PersistentPlayerSlot>(root, true)[index];
            var connection = em.CreateEntity(typeof(NetworkId), typeof(PlayerConnection));
            em.SetComponentData(connection, new NetworkId { Value = index + 1 });
            em.SetComponentData(connection, new PlayerConnection { PlayerId = slot.PlayerId, MatchSessionId = 100, JoinGeneration = 1, Epoch = 7 });
            if (isReady) em.AddComponentData(connection, new PlayerReady { MatchSessionId = 100, JoinGeneration = 1, Epoch = 7 });
            slot.Claimed = true; slot.Connection = connection; slot.JoinGeneration = 1;
            slot.State = isReady ? PlayerSlotState.Ready : PlayerSlotState.Loading;
            var current = em.GetBuffer<PersistentPlayerSlot>(root); current[index] = slot;
            return connection;
        }
    }

    static void CheckManifest()
    {
        using var world = new World("Mutable bootstrap manifest", WorldFlags.GameClient);
        var em = world.EntityManager;
        var connection = em.CreateEntity(typeof(RequiredBootstrapGhost));
        var ghost = em.CreateEntity(typeof(GhostInstance));
        em.SetComponentData(ghost, new GhostInstance { ghostId = 10, spawnTick = new NetworkTick(1) });
        em.GetBuffer<RequiredBootstrapGhost>(connection).Add(new RequiredBootstrapGhost { GhostId = 10, SpawnTick = new NetworkTick(1) });
        PlayerBootstrapReceipt.Observe(em, connection); em.DestroyEntity(ghost);
        Assert(PlayerBootstrapReceipt.Complete(em.GetBuffer<RequiredBootstrapGhost>(connection), 1), "A received ghost must remain satisfied after despawn");
        var missing = new RequiredBootstrapGhost { GhostId = 20, SpawnTick = new NetworkTick(2) };
        em.GetBuffer<RequiredBootstrapGhost>(connection).Add(missing);
        Assert(!PlayerBootstrapReceipt.Complete(em.GetBuffer<RequiredBootstrapGhost>(connection), 2), "An unseen authorized ghost must hold the barrier");
        missing.Withdrawn = true; var required = em.GetBuffer<RequiredBootstrapGhost>(connection); required[1] = missing;
        Assert(PlayerBootstrapReceipt.Complete(required, 2), "Server withdrawal must release a dead/hidden ghost without pinning visibility");
        Assert(!PlayerBootstrapReceipt.Complete(required, 3), "A partial manifest must never pass");
        var current = em.CreateEntity(typeof(NetworkId), typeof(ClientPlayerState), typeof(RequiredBootstrapGhost));
        em.SetComponentData(current, new NetworkId { Value = 17 });
        em.SetComponentData(current, new ClientPlayerState { PlayerId = 2, MatchSessionId = 100, JoinGeneration = 1, Epoch = 7, ExpectedGhosts = -1 });
        var stale = em.CreateEntity(typeof(PlayerBootstrapGhostRpc), typeof(ReceiveRpcCommandRequest));
        em.SetComponentData(stale, new PlayerBootstrapGhostRpc { MatchSessionId = 100, JoinGeneration = 1, Epoch = 7, GhostId = 99, SpawnTick = new NetworkTick(1) });
        em.SetComponentData(stale, new ReceiveRpcCommandRequest { SourceConnection = connection });
        world.GetOrCreateSystemManaged<PlayerHandshakeReceiveSystem>().Update();
        Assert(!em.Exists(stale) && em.GetBuffer<RequiredBootstrapGhost>(current).Length == 0, "A previous transport must never contaminate the current bootstrap manifest");
    }

    static void CheckTicketPersistence()
    {
        string directory = Path.Combine(Path.GetTempPath(), "RTSRejoinChecks-" + Guid.NewGuid().ToString("N"));
        try
        {
            var ticket = PlayerResumeTicket.Create();
            Assert(PlayerResumeStore.SaveAt(directory, "127.0.0.1:7979", 100, ticket, 2, "Alice"), "Private client ticket must save outside the project");
            Assert(PlayerResumeStore.LookupAt(directory, "127.0.0.1:7979", "Alice", -1, out var receipt) == ResumeLookup.Found
                && receipt.MatchSessionId == 100 && receipt.Ticket == ticket.ToString(), "A fresh process must recover its endpoint/match credential from disk");
            Assert(PlayerResumeStore.LookupAt(directory, "127.0.0.1:7980", "Alice", -1, out _) == ResumeLookup.None, "Another endpoint must not receive this ticket");
            var bob = PlayerResumeTicket.Create();
            Assert(PlayerResumeStore.SaveAt(directory, "127.0.0.1:7979", 100, bob, 3, "Bob"), "A second local player must retain a separate credential");
            Assert(PlayerResumeStore.LookupAt(directory, "127.0.0.1:7979", "Alice", -1, out receipt) == ResumeLookup.Found && receipt.Ticket == ticket.ToString(), "Another local client's save must never overwrite Alice's ticket");
            Assert(PlayerResumeStore.LookupAt(directory, "127.0.0.1:7979", "Bob", -1, out receipt) == ResumeLookup.Found && receipt.Ticket == bob.ToString(), "An explicit local profile must select the intended player");
            Assert(PlayerResumeStore.LookupAt(directory, "127.0.0.1:7979", "", -1, out _) == ResumeLookup.ProfileRequired, "Profile selection must never silently choose another local player");
            Assert(PlayerResumeStore.SaveAt(directory, "127.0.0.1:7979", 100, bob, 3, "Alice"), "Even duplicate profiles must preserve distinct player receipts");
            Assert(PlayerResumeStore.LookupAt(directory, "127.0.0.1:7979", "Alice", -1, out _) == ResumeLookup.Ambiguous, "Multiple tickets in one profile must require explicit player choice");
            Assert(PlayerResumeStore.LookupAt(directory, "127.0.0.1:7979", "Alice", 2, out receipt) == ResumeLookup.Found && receipt.Ticket == ticket.ToString(), "Explicit player choice must recover the original identity without overwriting another slot");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
                Directory.Delete(directory);
            }
        }
    }

    static void CheckOwnedClientDisconnect()
    {
        using var first = new World("Owned client", WorldFlags.GameClient);
        using var sibling = new World("Sibling client", WorldFlags.GameClient);
        using var server = new World("Preserved server", WorldFlags.GameServer);
        var firstConnection = first.EntityManager.CreateEntity(typeof(NetworkStreamConnection));
        var siblingConnection = sibling.EntityManager.CreateEntity(typeof(NetworkStreamConnection));
        var owner = new GameObject("Client owner check"); owner.SetActive(false);
        try
        {
            var ui = owner.AddComponent<Unity.Multiplayer.Center.NetcodeForEntitiesSetup.ConnectionUI>();
            typeof(Unity.Multiplayer.Center.NetcodeForEntitiesSetup.ConnectionUI).GetField("ownedClient", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(ui, first);
            ui.Disconnect();
            Assert(first.EntityManager.HasComponent<NetworkStreamRequestDisconnect>(firstConnection), "Disconnect must target the UI-owned client");
            Assert(!sibling.EntityManager.HasComponent<NetworkStreamRequestDisconnect>(siblingConnection) && sibling.IsCreated && server.IsCreated,
                "Another local client and the host server must remain intact");
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }

    public static void CheckSessionCanvasLifetime()
    {
        var owner = new GameObject("Session canvas lifetime check", typeof(RectTransform), typeof(Canvas));
        owner.SetActive(false);
        GameObject controls = null;
        try
        {
            var ui = owner.AddComponent<Unity.Multiplayer.Center.NetcodeForEntitiesSetup.ConnectionUI>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            ui.GetType().GetMethod("EnsureSessionControls", flags).Invoke(ui, null);
            controls = ((Canvas)ui.GetType().GetField("sessionCanvas", flags).GetValue(ui)).gameObject;
            Assert(!controls.transform.IsChildOf(owner.transform) && controls.activeInHierarchy,
                "Session controls must remain visible when the frontend parent canvas is hidden");
            Assert(controls.scene == owner.scene, "Session controls must follow the owner's scene lifetime");
        }
        finally
        {
            if (controls) UnityEngine.Object.DestroyImmediate(controls);
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    static void CheckRetryRequiresReceipt()
    {
        using var client = new World("Unchanged client without receipt", WorldFlags.GameClient);
        using var server = new World("Unchanged server without receipt", WorldFlags.GameServer);
        var owner = new GameObject("Missing receipt retry check"); owner.SetActive(false);
        try
        {
            var ui = owner.AddComponent<Unity.Multiplayer.Center.NetcodeForEntitiesSetup.ConnectionUI>();
            ui.LocalProfile = "Check-" + Guid.NewGuid().ToString("N").Substring(0, 20);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            ui.GetType().GetField("ownedClient", flags).SetValue(ui, client);
            int count = World.All.Count;
            ui.RetryConnection(); ui.GetType().GetMethod("Update", flags).Invoke(ui, null);
            Assert(client.IsCreated && server.IsCreated && World.All.Count == count,
                "Rejoin without a saved receipt must not replace any World or act as a fresh join");
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }
}
