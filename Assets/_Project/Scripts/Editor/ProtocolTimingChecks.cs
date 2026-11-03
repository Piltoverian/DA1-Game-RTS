using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEditor;
using UnityEngine;

public static class ProtocolTimingChecks
{
    static int checks;

    [MenuItem("Tools/RTS/Check Protocol Timing")]
    public static void Run()
    {
        checks = 0;
        CheckColdClientPrefabGate();
        CheckIndependentLoadingDeadlines();
        CheckDeliveredCommandDrain();
        CheckLoadingInputGate();
        CheckKnownNeutralValidation();
        CheckBoundedOverload();
        CheckManifestBudgetAndWithdrawal();
        Debug.Log($"Protocol timing checks passed: {checks} assertions.");
    }

    static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(message);
    }

    static void CheckColdClientPrefabGate()
    {
        using var world = new World("Cold second client", WorldFlags.GameClient);
        var em = world.EntityManager;
        var catalog = ScriptableObject.CreateInstance<GameDataRegistry>();
        try
        {
            var connection = em.CreateEntity(typeof(NetworkId));
            var endpoint = em.CreateEntity(typeof(ClientConnectionEndpoint));
            em.SetComponentData(endpoint, new ClientConnectionEndpoint { FreshJoin = true, Profile = "cold-client-check" });
            var join = world.GetOrCreateSystem<PlayerJoinClientSystem>();
            var group = world.GetOrCreateSystemManaged<SimulationSystemGroup>();
            group.AddSystemToUpdateList(join);
            group.Update();
            Check(!em.HasComponent<PlayerJoinSent>(connection) && !em.HasComponent<NetworkStreamInGame>(connection),
                "A cold client must not join or receive snapshots before its local catalog loads");
            var registry = em.CreateEntity(typeof(GameDataRegistryComponent), typeof(PresentationCatalogReference));
            em.SetComponentData(registry, new PresentationCatalogReference { Catalog = catalog });
            em.AddBuffer<RegistryBlobElement>(registry); em.AddBuffer<RegistryTechEffectElement>(registry);
            em.AddBuffer<RegistryPrefabElement>(registry);
            var root = em.CreateEntity(typeof(NetworkGameplayPrefabs), typeof(TerrainSyncSettings));
            var prefab = em.CreateEntity(typeof(Prefab), typeof(GhostType), typeof(GhostInstance));
            var missing = em.CreateEntity();
            em.SetComponentData(root, new NetworkGameplayPrefabs { Match = prefab, PlayerContext = prefab });
            em.SetComponentData(root, new TerrainSyncSettings { Prefab = missing });
            group.Update();
            Check(!em.HasComponent<NetworkStreamInGame>(connection), "A loaded catalog alone must not release an unresolved terrain prefab");
            em.SetComponentData(root, new TerrainSyncSettings { Prefab = prefab });
            em.AddBuffer<MapResourceGhostPrefabReference>(root).Add(new MapResourceGhostPrefabReference { Value = missing });
            group.Update();
            Check(!em.HasComponent<PlayerJoinSent>(connection), "Resource prefab references must load before Join");
            em.AddComponent<Prefab>(missing); em.AddComponent<GhostType>(missing); em.AddComponent<GhostInstance>(missing);
            group.Update();
            using var requests = em.CreateEntityQuery(typeof(PlayerJoinRequest));
            Check(em.HasComponent<PlayerJoinSent>(connection) && em.HasComponent<NetworkStreamInGame>(connection)
                && requests.CalculateEntityCount() == 1, "Once local prefabs load, client must send exactly one Join and enable snapshots");
            group.Update();
            Check(requests.CalculateEntityCount() == 1, "Readiness retries must not duplicate Join");
            var stalled = em.CreateEntity(typeof(NetworkId));
            var clock = em.CreateEntity(typeof(PlayerProtocolClockOverride));
            em.SetComponentData(clock, new PlayerProtocolClockOverride { Now = 0 });
            em.DestroyEntity(missing);
            group.Update();
            em.SetComponentData(clock, new PlayerProtocolClockOverride { Now = 60 });
            group.Update();
            Check(em.HasComponent<NetworkStreamRequestDisconnect>(stalled)
                && em.GetComponentData<ClientJoinFailure>(stalled).Reason == PlayerJoinRejection.LocalPrefabsTimedOut,
                "A genuinely missing local prefab must fail explicitly after the loading deadline");
        }
        finally { UnityEngine.Object.DestroyImmediate(catalog); }
    }

    static void CheckIndependentLoadingDeadlines()
    {
        using var world = new World("Wall-clock loading deadlines", WorldFlags.GameServer);
        var em = world.EntityManager;
        var root = em.CreateEntity(typeof(PlayerBootstrapSlot));
        var context = em.CreateEntity();
        em.GetBuffer<PlayerBootstrapSlot>(root).Add(new PlayerBootstrapSlot { PlayerId = 2, Context = context });
        PlayerSessionSlots.Ensure(em, 100, out _);
        var match = em.CreateEntity(typeof(MatchSession), typeof(MatchState));
        em.SetComponentData(match, new MatchSession { Id = 100 });
        em.SetComponentData(match, new MatchState { Phase = MatchPhase.Running });
        var clock = em.CreateEntity(typeof(PlayerProtocolClockOverride));
        em.SetComponentData(clock, new PlayerProtocolClockOverride { Now = 0 });
        Check(!PlayerSessionSlots.AdvanceWaiting(em, root, PlayerProtocolClock.Now(em), 7), "Absent slot must wait for the roster deadline");
        var connection = Admit(29, 1);
        Check(PlayerSessionSlots.AdvanceWaiting(em, root, 30, 7), "Roster cutoff must start the match while admitted clients finish loading");
        Check(PlayerSessionSlots.TryGetSlot(em, 2, out var slot) && slot.LoadingDeadline == 89
            && !em.HasComponent<NetworkStreamRequestDisconnect>(connection), "A second-29 admission must receive the full default 60 seconds");
        var lifecycle = world.GetOrCreateSystemManaged<PlayerSessionLifecycleSystem>();
        world.SetTime(new Unity.Core.TimeData(0, 0));
        Tick(88.99);
        Check(!em.HasComponent<NetworkStreamRequestDisconnect>(connection), "Paused gameplay must not shorten the independent loading window");
        Tick(89);
        Check(em.HasComponent<NetworkStreamRequestDisconnect>(connection) && PlayerSessionSlots.TryGetSlot(em, 2, out slot)
            && slot.Claimed && slot.Context == context && slot.LoadingFailure == PlayerLoadingFailure.LoadingTimedOut,
            "Stalled loading must expire in Running using wall time, retaining claim/context and a reason");
        var rejoin = Admit(100, 2);
        Tick(159.99);
        Check(!em.HasComponent<NetworkStreamRequestDisconnect>(rejoin), "A Running rejoin must own a new full loading window");
        Tick(160);
        Check(em.HasComponent<NetworkStreamRequestDisconnect>(rejoin), "A stalled Running rejoin must eventually expire even with paused simulation time");

        Entity Admit(double now, uint generation)
        {
            var transport = em.CreateEntity(typeof(NetworkId), typeof(PlayerConnection));
            em.SetComponentData(transport, new NetworkId { Value = (int)generation });
            em.SetComponentData(transport, new PlayerConnection { PlayerId = 2, MatchSessionId = 100, JoinGeneration = generation, Epoch = 7 });
            var current = em.GetBuffer<PersistentPlayerSlot>(root, true)[0];
            current.Connection = transport; current.Claimed = true; current.JoinGeneration = generation;
            PlayerSessionSlots.BeginLoading(em, root, ref current, now);
            var slots = em.GetBuffer<PersistentPlayerSlot>(root);
            slots[0] = current;
            return transport;
        }
        void Tick(double now)
        {
            em.SetComponentData(clock, new PlayerProtocolClockOverride { Now = now });
            lifecycle.Update();
        }
    }

    static void CheckDeliveredCommandDrain()
    {
        using var f = new CommandFixture();
        for (uint i = 1; i <= 4097; i++) f.Submit(i);
        f.Server.Update();
        Check(f.Queued == 4096, "The first update must process exactly the command budget");
        Check(f.Results == 4096, "Every processed command must receive its explicit receipt");
        f.Submit(4098);
        f.Server.Update();
        Check(f.Queued == 4098 && f.Results == 4098, "Request 4097 must drain next update along with newly delivered requests, without loss");
        var commands = f.Em.GetBuffer<CommandQueueElement>(f.Queue);
        Check(commands[4096].position.x == 4097 && commands[4097].position.x == 4098
            && f.Em.GetComponentData<NetworkWorkerCommand>(f.Unit).RequestId == 4098,
            "Deferred commands must preserve FIFO and source supersession order across updates");
    }

    static void CheckBoundedOverload()
    {
        using var f = new CommandFixture();
        for (uint i = 1; i <= GameplayCommandServerSystem.MaxPendingCommands + 17; i++) f.Submit(i);
        f.Server.Update();
        using var replies = f.Em.CreateEntityQuery(typeof(GameplayCommandResultRpc), typeof(SendRpcCommandRequest));
        Check(replies.CalculateEntityCount() == 1 && replies.GetSingleton<GameplayCommandResultRpc>().RejectPending
            && replies.GetSingleton<GameplayCommandResultRpc>().Result == CommandReceipt.RateLimited,
            "Hard overload must explicitly reject the undrained transport batch with one bounded response");
        Check(f.Queued == 0 && f.Em.HasComponent<PlayerJoinDenied>(f.Connection), "Overflow must retire pending commands and schedule transport shutdown");
        f.Server.Update();
        Check(f.Queued == 0 && f.Results == 1, "Rejected overload must not silently replay or amplify responses on later ticks");
    }

    static void CheckLoadingInputGate()
    {
        using var f = new CommandFixture();
        f.Em.RemoveComponent<PlayerReady>(f.Connection);
        f.Submit(1); f.Server.Update();
        using var results = f.Em.CreateEntityQuery(typeof(GameplayCommandResultRpc));
        Check(f.Queued == 0 && results.GetSingleton<GameplayCommandResultRpc>().Result == CommandReceipt.InvalidSession,
            "Running must keep admitted Loading clients input-gated until the server Ready barrier");
        f.Em.AddComponentData(f.Connection, new PlayerReady { MatchSessionId = 100, JoinGeneration = 3, Epoch = 5 });
        f.Submit(2); f.Server.Update();
        Check(f.Queued == 1, "The same transport must gain command authority after its matching Ready barrier");
    }

    static void CheckKnownNeutralValidation()
    {
        using var f = new CommandFixture();
        f.Em.SetComponentData(f.Unit, new EntityOwner { PlayerID = 7 });
        f.Submit(1, CommandType.TargetTo, 1); f.Server.Update();
        Check(f.Queued == 0 && Result(1) == CommandReceipt.NotOwner, "Known-neutral commands must not control another player's actor");
        f.Em.SetComponentData(f.Unit, new EntityOwner { PlayerID = 2 });
        f.Submit(2, CommandType.Move, 1); f.Server.Update();
        Check(f.Queued == 0 && Result(2) == CommandReceipt.InvalidTarget, "Known-neutral identity must not be accepted on an unrelated command type");
        f.Submit(3, CommandType.TargetTo, ulong.MaxValue); f.Server.Update();
        Check(f.Queued == 0 && Result(3) == CommandReceipt.InvalidTarget, "Unknown neutral IDs must fail without resolving hidden entities");

        CommandReceipt Result(uint id)
        {
            using var q = f.Em.CreateEntityQuery(typeof(GameplayCommandResultRpc));
            using var replies = q.ToComponentDataArray<GameplayCommandResultRpc>(Allocator.Temp);
            foreach (var reply in replies) if (reply.RequestId == id) return reply.Result;
            throw new InvalidOperationException("Missing known-neutral validation receipt");
        }
    }

    static void CheckManifestBudgetAndWithdrawal()
    {
        using var world = new World("Budgeted manifest", WorldFlags.GameServer);
        var em = world.EntityManager;
        var connection = em.CreateEntity(typeof(PlayerConnection), typeof(PlayerBootstrapManifestState), typeof(RequiredBootstrapGhost));
        var player = new PlayerConnection { PlayerId = 2, MatchSessionId = 100, JoinGeneration = 3, Epoch = 7 };
        em.SetComponentData(connection, player);
        for (int i = 1; i <= 300; i++) em.GetBuffer<RequiredBootstrapGhost>(connection).Add(new RequiredBootstrapGhost
        { GhostId = i, SpawnTick = new NetworkTick(1) });
        PlayerBootstrapManifest.Drain(em, connection, ref player);
        using var entries = em.CreateEntityQuery(typeof(PlayerBootstrapGhostRpc), typeof(SendRpcCommandRequest));
        using var summaries = em.CreateEntityQuery(typeof(PlayerBootstrapSummaryRpc));
        Check(entries.CalculateEntityCount() == PlayerBootstrapManifest.MaxMessagesPerUpdate && !player.BootstrapSent
            && summaries.IsEmptyIgnoreFilter, "Initial manifest must respect its per-update message budget and withhold completion");
        var future = em.GetBuffer<RequiredBootstrapGhost>(connection, true)[299]; future.Withdrawn = true;
        var manifest = em.GetBuffer<RequiredBootstrapGhost>(connection);
        manifest[299] = future;
        PlayerBootstrapManifest.Drain(em, connection, ref player);
        Check(entries.CalculateEntityCount() == 256 && !player.BootstrapSent, "Large manifests must continue over bounded updates");
        PlayerBootstrapManifest.Drain(em, connection, ref player);
        Check(entries.CalculateEntityCount() == 300 && player.BootstrapSent && summaries.CalculateEntityCount() == 1,
            "Summary must follow the complete paged manifest exactly once");
        bool withdrawn = false;
        using var values = entries.ToComponentDataArray<PlayerBootstrapGhostRpc>(Allocator.Temp);
        foreach (var value in values) if (value.GhostId == 300) withdrawn = value.Withdrawn;
        Check(withdrawn, "A future manifest page must transmit withdrawal instead of pinning a newly hidden ghost");
    }

    sealed class CommandFixture : IDisposable
    {
        public readonly World World = new("Delivered RPC drain", WorldFlags.GameServer);
        public EntityManager Em => World.EntityManager;
        public readonly Entity Queue, Connection, Unit;
        public readonly GameplayCommandServerSystem Server;
        public int Queued => Em.GetBuffer<CommandQueueElement>(Queue).Length;
        public int Results { get { using var q = Em.CreateEntityQuery(typeof(GameplayCommandResultRpc)); return q.CalculateEntityCount(); } }

        public CommandFixture()
        {
            var match = Em.CreateEntity(typeof(MatchState)); Em.SetComponentData(match, new MatchState { Phase = MatchPhase.Running });
            var terrain = Em.CreateEntity(); Em.AddComponentObject(terrain, new TerrainVisualSource { Epoch = 5 });
            Queue = Em.CreateEntity(typeof(CommandQueueComponent), typeof(CommandQueueElement));
            Connection = Em.CreateEntity(typeof(NetworkId), typeof(PlayerConnection), typeof(PlayerReady));
            Em.SetComponentData(Connection, new NetworkId { Value = 17 });
            Em.SetComponentData(Connection, new PlayerConnection { PlayerId = 2, MatchSessionId = 100, JoinGeneration = 3, Epoch = 5 });
            Em.SetComponentData(Connection, new PlayerReady { MatchSessionId = 100, JoinGeneration = 3, Epoch = 5 });
            Unit = Em.CreateEntity(typeof(GhostInstance), typeof(EntityOwner), typeof(MoveOverride));
            Em.SetComponentData(Unit, new GhostInstance { ghostId = 10, spawnTick = new NetworkTick(1) });
            Em.SetComponentData(Unit, new EntityOwner { PlayerID = 2 });
            Server = World.GetOrCreateSystemManaged<GameplayCommandServerSystem>();
        }

        public void Submit(uint id, CommandType type = CommandType.Move, ulong knownNeutralId = 0)
        {
            var rpc = Em.CreateEntity(typeof(GameplayCommandRpc), typeof(ReceiveRpcCommandRequest));
            Em.SetComponentData(rpc, new GameplayCommandRpc { MatchSessionId = 100, JoinGeneration = 3, Epoch = 5,
                RequestId = id, Type = type, KnownNeutralId = knownNeutralId, SourceId = 10, SourceTick = new NetworkTick(1), Position = new float3(id, 0, 0) });
            Em.SetComponentData(rpc, new ReceiveRpcCommandRequest { SourceConnection = Connection });
        }

        public void Dispose() => World.Dispose();
    }
}
