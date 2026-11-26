using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

public struct PlayerJoinRequest : IRpcCommand
{
    public ulong ResumeMatchSessionId;
    public FixedString128Bytes ResumeTicket;
}
public struct PlayerJoinSent : IComponentData { }
public struct ClientPrefabLoadDeadline : IComponentData { public double Value; }
public struct PlayerResumeTicketRpc : IRpcCommand
{
    public ulong MatchSessionId;
    public uint JoinGeneration;
    public FixedString128Bytes Ticket;
}
public enum PlayerJoinRejection : byte { InvalidTicket, WrongMatch, NoAvailableSlot, ProfileRequired, AmbiguousLocalProfile, TicketUnavailable, LocalPrefabsTimedOut }
public struct PlayerJoinRejectedRpc : IRpcCommand { public PlayerJoinRejection Reason; }
public struct ClientJoinFailure : IComponentData { public PlayerJoinRejection Reason; }
public struct PlayerJoinDenied : IComponentData { public double DisconnectAfter; }

// Stored on the authenticated NetCode connection, never supplied by the client.
public struct PlayerConnection : IComponentData
{
    public int PlayerId;
    public ulong MatchSessionId;
    public uint JoinGeneration;
    public uint Epoch;
    public bool BootstrapSent;
}

public struct PlayerJoinAcceptedRpc : IRpcCommand
{
    public int PlayerId;
    public ulong MatchSessionId;
    public uint JoinGeneration;
    public uint Epoch;
}
public struct PlayerBootstrapGhostRpc : IRpcCommand
{
    public ulong MatchSessionId;
    public uint JoinGeneration, Epoch;
    public int GhostId;
    public NetworkTick SpawnTick;
    public bool Withdrawn;
}
public struct PlayerBootstrapSummaryRpc : IRpcCommand
{
    public ulong MatchSessionId;
    public uint JoinGeneration, Epoch;
    public int Count;
}
public struct PlayerReadyRequest : IRpcCommand
{
    public ulong MatchSessionId;
    public uint JoinGeneration, Epoch;
}
public struct PlayerReadyAcceptedRpc : IRpcCommand
{
    public ulong MatchSessionId;
    public uint JoinGeneration, Epoch;
}
public struct PlayerReady : IComponentData
{
    public ulong MatchSessionId;
    public uint JoinGeneration, Epoch;
}
public struct RequiredBootstrapGhost : IBufferElementData
{
    public int GhostId;
    public NetworkTick SpawnTick;
    public bool Received, Withdrawn, WithdrawalSent;
}
public struct PlayerBootstrapManifestState : IComponentData { public int Cursor; }
public struct ObservedBootstrapGhost : IBufferElementData
{
    public int GhostId;
    public NetworkTick SpawnTick;
}
public enum ClientLoadingPhase : byte { Receiving, ReadySent, ServerReady }
public struct ClientPlayerState : IComponentData
{
    public int PlayerId;
    public ulong MatchSessionId;
    public uint JoinGeneration, Epoch;
    public int ExpectedGhosts;
    public uint InitialTerrainVersion;
    public ClientLoadingPhase Phase;
}

public static class PlayerNetwork
{
    public static int CompareVersion(in ClientPlayerState state, ulong session, uint join, uint epoch)
    {
        int order = session.CompareTo(state.MatchSessionId);
        if (order == 0) order = join.CompareTo(state.JoinGeneration);
        if (order == 0) order = epoch.CompareTo(state.Epoch);
        return order;
    }
    public static bool Matches(in ClientPlayerState state, ulong session, uint join, uint epoch) =>
        state.MatchSessionId == session && state.JoinGeneration == join && state.Epoch == epoch;

    public static bool TryLocal(World world, out Entity connection, out ClientPlayerState player, bool requireReady = true)
    {
        connection = Entity.Null; player = default;
        if (world == null || !world.IsCreated || !world.IsClient()) return false;
        using var query = world.EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<NetworkId>(), ComponentType.ReadOnly<ClientPlayerState>() },
            None = new[] { ComponentType.ReadOnly<NetworkStreamRequestDisconnect>() }
        });
        if (query.CalculateEntityCount() != 1) return false;
        connection = query.GetSingletonEntity(); player = query.GetSingleton<ClientPlayerState>();
        return !requireReady || player.Phase == ClientLoadingPhase.ServerReady;
    }

    public static void Send<T>(EntityManager em, Entity connection, T value) where T : unmanaged, IRpcCommand
    {
        var request = em.CreateEntity(typeof(T), typeof(SendRpcCommandRequest));
        if (!TypeManager.GetTypeInfo<T>().IsZeroSized) em.SetComponentData(request, value);
        em.SetComponentData(request, new SendRpcCommandRequest { TargetConnection = connection });
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct PlayerJoinClientSystem : ISystem
{
    public void OnCreate(ref SystemState state) => state.RequireForUpdate<NetworkId>();
    public void OnUpdate(ref SystemState state)
    {
        // A running server can send its entire prefab table on the first snapshot.
        // Do not advertise InGame while the client's local SubScene is still streaming.
        bool prefabsReady = PlayerClientPrefabReadiness.IsReady(state.EntityManager);
        using var query = state.EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<NetworkId>() },
            None = new[] { ComponentType.ReadOnly<PlayerJoinSent>(), ComponentType.ReadOnly<NetworkStreamRequestDisconnect>() }
        });
        using var connections = query.ToEntityArray(Allocator.Temp);
        foreach (var connection in connections)
        {
            if (!prefabsReady)
            {
                double now = PlayerProtocolClock.Now(state.EntityManager);
                if (!state.EntityManager.HasComponent<ClientPrefabLoadDeadline>(connection))
                    state.EntityManager.AddComponentData(connection, new ClientPrefabLoadDeadline { Value = now + 60 });
                else if (now >= state.EntityManager.GetComponentData<ClientPrefabLoadDeadline>(connection).Value)
                {
                    state.EntityManager.AddComponentData(connection, new ClientJoinFailure { Reason = PlayerJoinRejection.LocalPrefabsTimedOut });
                    state.EntityManager.AddComponent<NetworkStreamRequestDisconnect>(connection);
                }
                continue;
            }
            if (state.EntityManager.HasComponent<ClientPrefabLoadDeadline>(connection))
                state.EntityManager.RemoveComponent<ClientPrefabLoadDeadline>(connection);
            state.EntityManager.AddComponent<PlayerJoinSent>(connection);
            if (!state.EntityManager.HasComponent<NetworkStreamInGame>(connection))
                state.EntityManager.AddComponent<NetworkStreamInGame>(connection);
            var request = new PlayerJoinRequest();
            using var endpoint = state.EntityManager.CreateEntityQuery(typeof(ClientConnectionEndpoint));
            var selection = endpoint.CalculateEntityCount() == 1 ? endpoint.GetSingleton<ClientConnectionEndpoint>() : default;
            PlayerResumeStore.Receipt resume = null;
            var lookup = selection.Profile.IsEmpty ? ResumeLookup.ProfileRequired : selection.FreshJoin ? ResumeLookup.None
                : PlayerResumeStore.Lookup(PlayerResumeStore.Endpoint(state.EntityManager, connection), selection.Profile.ToString(), selection.ResumePlayerId, out resume);
            if (lookup == ResumeLookup.ProfileRequired || lookup == ResumeLookup.Ambiguous || lookup == ResumeLookup.Unavailable
                || lookup == ResumeLookup.None && selection.ResumePlayerId >= 0 && !selection.FreshJoin)
            {
                state.EntityManager.AddComponentData(connection, new ClientJoinFailure { Reason = lookup == ResumeLookup.ProfileRequired ? PlayerJoinRejection.ProfileRequired
                    : lookup == ResumeLookup.Ambiguous ? PlayerJoinRejection.AmbiguousLocalProfile : PlayerJoinRejection.TicketUnavailable });
                state.EntityManager.AddComponent<NetworkStreamRequestDisconnect>(connection);
                continue;
            }
            if (lookup == ResumeLookup.Found)
            {
                request.ResumeMatchSessionId = resume.MatchSessionId;
                request.ResumeTicket = new FixedString128Bytes(resume.Ticket);
            }
            PlayerNetwork.Send(state.EntityManager, connection, request);
        }
    }
}

public static class PlayerClientPrefabReadiness
{
    public static bool IsReady(EntityManager em)
    {
        if (!ClientPresentationCatalog.TryGet(em, out var registry, out _)) return false;
        using var gameplay = em.CreateEntityQuery(typeof(NetworkGameplayPrefabs));
        using var terrain = em.CreateEntityQuery(typeof(TerrainSyncSettings));
        if (gameplay.CalculateEntityCount() != 1 || terrain.CalculateEntityCount() != 1) return false;
        var required = gameplay.GetSingleton<NetworkGameplayPrefabs>();
        if (!GhostReady(required.Match) || !GhostReady(required.PlayerContext)
            || !GhostReady(terrain.GetSingleton<TerrainSyncSettings>().Prefab)) return false;
        foreach (var entry in em.GetBuffer<RegistryPrefabElement>(registry, true))
            if (!em.Exists(entry.Prefab) || !em.HasComponent<Prefab>(entry.Prefab)
                || em.HasComponent<GhostInstance>(entry.Prefab) && !GhostReady(entry.Prefab)) return false;
        using var resources = em.CreateEntityQuery(typeof(MapResourceGhostPrefabReference));
        using var roots = resources.ToEntityArray(Allocator.Temp);
        foreach (var root in roots)
            foreach (var entry in em.GetBuffer<MapResourceGhostPrefabReference>(root, true))
                if (!GhostReady(entry.Value)) return false;
        return true;

        bool GhostReady(Entity prefab) => em.Exists(prefab) && em.HasComponent<Prefab>(prefab)
            && em.HasComponent<GhostType>(prefab) && em.HasComponent<GhostInstance>(prefab);
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(TerrainNetworkServerSystem))]
public partial class PlayerJoinServerSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<PlayerBootstrapSlot>(); RequireForUpdate<MatchSession>();
        RequireForUpdate<TerrainVisualSource>(); RequireForUpdate<PlayerBootstrapState>();
    }
    protected override void OnUpdate()
    {
        if (SystemAPI.GetSingleton<PlayerBootstrapState>().Phase != PlayerBootstrapPhase.Ready) return;
        using var sourceQuery = EntityManager.CreateEntityQuery(typeof(TerrainVisualSource));
        if (sourceQuery.CalculateEntityCount() != 1) return;
        uint epoch = EntityManager.GetComponentObject<TerrainVisualSource>(sourceQuery.GetSingletonEntity()).Epoch;
        ulong session = SystemAPI.GetSingleton<MatchSession>().Id;
        if (!PlayerSessionSlots.Ensure(EntityManager, session, out var root)) return;
        using var requests = EntityManager.CreateEntityQuery(typeof(PlayerJoinRequest), typeof(ReceiveRpcCommandRequest));
        using var entities = requests.ToEntityArray(Allocator.Temp);
        foreach (var request in entities)
        {
            var join = EntityManager.GetComponentData<PlayerJoinRequest>(request);
            var connection = EntityManager.GetComponentData<ReceiveRpcCommandRequest>(request).SourceConnection;
            if (EntityManager.Exists(connection) && EntityManager.HasComponent<NetworkId>(connection)
                && !EntityManager.HasComponent<NetworkStreamRequestDisconnect>(connection)
                && !EntityManager.HasComponent<PlayerJoinDenied>(connection))
            {
                if (!EntityManager.HasComponent<PlayerConnection>(connection))
                {
                    int selected = -1;
                    var slots = EntityManager.GetBuffer<PersistentPlayerSlot>(root, true);
                    bool resuming = join.ResumeMatchSessionId != 0 || !join.ResumeTicket.IsEmpty;
                    var rejection = resuming ? PlayerJoinRejection.InvalidTicket : PlayerJoinRejection.NoAvailableSlot;
                    if (resuming && join.ResumeMatchSessionId != session) rejection = PlayerJoinRejection.WrongMatch;
                    else for (int i = 0; i < slots.Length; i++)
                    {
                        var candidate = slots[i];
                        if (resuming ? candidate.Claimed && PlayerResumeTicket.Matches(join.ResumeTicket, candidate.TicketHash)
                            : !candidate.Claimed && (selected < 0 || candidate.PlayerId < slots[selected].PlayerId)) selected = i;
                        if (resuming && selected >= 0) break;
                    }
                    if (selected < 0)
                    {
                        PlayerNetwork.Send(EntityManager, connection, new PlayerJoinRejectedRpc { Reason = rejection });
                        EntityManager.AddComponentData(connection, new PlayerJoinDenied { DisconnectAfter = PlayerProtocolClock.Now(EntityManager) + .5 });
                    }
                    else
                    {
                        var slot = slots[selected];
                        if (slot.JoinGeneration == uint.MaxValue)
                        {
                            PlayerNetwork.Send(EntityManager, connection, new PlayerJoinRejectedRpc { Reason = PlayerJoinRejection.InvalidTicket });
                            EntityManager.AddComponentData(connection, new PlayerJoinDenied { DisconnectAfter = PlayerProtocolClock.Now(EntityManager) + .5 });
                            EntityManager.DestroyEntity(request); continue;
                        }
                        var ticket = resuming ? join.ResumeTicket : PlayerResumeTicket.Create();
                        PlayerResumeTicket.TryHash(ticket, out slot.TicketHash);
                        var previous = slot.Connection;
                        if (EntityManager.Exists(previous) && previous != connection)
                        {
                            if (EntityManager.HasComponent<PlayerReady>(previous)) EntityManager.RemoveComponent<PlayerReady>(previous);
                            if (!EntityManager.HasComponent<NetworkStreamRequestDisconnect>(previous))
                                EntityManager.AddComponent<NetworkStreamRequestDisconnect>(previous);
                        }
                        slot.Connection = connection; slot.Claimed = true; slot.State = PlayerSlotState.Loading; slot.JoinGeneration++;
                        PlayerSessionSlots.BeginLoading(EntityManager, root, ref slot, PlayerProtocolClock.Now(EntityManager));
                        var updatedSlots = EntityManager.GetBuffer<PersistentPlayerSlot>(root);
                        updatedSlots[selected] = slot;
                        EntityManager.AddComponentData(connection, new PlayerConnection
                        { PlayerId = slot.PlayerId, MatchSessionId = session, JoinGeneration = slot.JoinGeneration, Epoch = epoch });
                        PlayerNetwork.Send(EntityManager, connection, new PlayerResumeTicketRpc
                        { MatchSessionId = session, JoinGeneration = slot.JoinGeneration, Ticket = ticket });
                    }
                }
                if (EntityManager.HasComponent<PlayerConnection>(connection))
                {
                    if (!EntityManager.HasComponent<NetworkStreamInGame>(connection)) EntityManager.AddComponent<NetworkStreamInGame>(connection);
                    var player = EntityManager.GetComponentData<PlayerConnection>(connection);
                    PlayerNetwork.Send(EntityManager, connection, new PlayerJoinAcceptedRpc
                    { PlayerId = player.PlayerId, MatchSessionId = player.MatchSessionId, JoinGeneration = player.JoinGeneration, Epoch = player.Epoch });
                }
            }
            EntityManager.DestroyEntity(request);
        }
    }
}
