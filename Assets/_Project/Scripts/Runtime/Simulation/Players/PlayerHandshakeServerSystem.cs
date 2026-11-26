using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(GameplayGhostRelevancySystem))]
[UpdateBefore(typeof(GhostSendSystem))]
public partial class PlayerHandshakeServerSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<MatchSession>(); RequireForUpdate<TerrainVisualSource>();
        RequireForUpdate<PlayerBootstrapState>();
    }

    protected override void OnUpdate()
    {
        if (SystemAPI.GetSingleton<PlayerBootstrapState>().Phase != PlayerBootstrapPhase.Ready) return;
        EntityManager.CompleteAllTrackedJobs();
        using var sourceQuery = EntityManager.CreateEntityQuery(typeof(TerrainVisualSource));
        if (sourceQuery.CalculateEntityCount() != 1) return;
        uint epoch = EntityManager.GetComponentObject<TerrainVisualSource>(sourceQuery.GetSingletonEntity()).Epoch;
        bool fogReady = GameplayGhostVisibility.TryGetFog(EntityManager, out var grid, out var fog);
        using var connectionQuery = EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<NetworkId>(), ComponentType.ReadOnly<PlayerConnection>(), ComponentType.ReadOnly<NetworkStreamInGame>() },
            None = new[] { ComponentType.ReadOnly<NetworkStreamRequestDisconnect>() }
        });
        using var connections = connectionQuery.ToEntityArray(Allocator.Temp);
        using var ghostQuery = EntityManager.CreateEntityQuery(typeof(GhostInstance));
        using var ghosts = ghostQuery.ToEntityArray(Allocator.Temp);
        var ghostLookup = new Dictionary<SpawnedGhost, Entity>(ghosts.Length);
        foreach (var entity in ghosts)
        {
            var ghost = EntityManager.GetComponentData<GhostInstance>(entity);
            if (ghost.ghostId != 0) ghostLookup[new SpawnedGhost { ghostId = ghost.ghostId, spawnTick = ghost.spawnTick }] = entity;
        }
        foreach (var connection in connections)
        {
            var player = EntityManager.GetComponentData<PlayerConnection>(connection);
            if (!PlayerSessionSlots.IsCurrent(EntityManager, connection, player)) continue;
            if (player.Epoch != epoch)
            {
                player.Epoch = epoch; player.BootstrapSent = false;
                if (EntityManager.HasComponent<PlayerReady>(connection)) EntityManager.RemoveComponent<PlayerReady>(connection);
                if (EntityManager.HasComponent<PlayerBootstrapManifestState>(connection)) EntityManager.RemoveComponent<PlayerBootstrapManifestState>(connection);
                using var roster = EntityManager.CreateEntityQuery(typeof(PersistentPlayerSlot));
                if (roster.CalculateEntityCount() == 1)
                {
                    var root = roster.GetSingletonEntity();
                    var slots = EntityManager.GetBuffer<PersistentPlayerSlot>(root);
                    for (int i = 0; i < slots.Length; i++)
                        if (slots[i].Connection == connection)
                        { var slot = slots[i]; PlayerSessionSlots.BeginLoading(EntityManager, root, ref slot, PlayerProtocolClock.Now(EntityManager)); slots[i] = slot; break; }
                }
                EntityManager.SetComponentData(connection, player);
                PlayerNetwork.Send(EntityManager, connection, new PlayerJoinAcceptedRpc
                { PlayerId = player.PlayerId, MatchSessionId = player.MatchSessionId, JoinGeneration = player.JoinGeneration, Epoch = epoch });
            }
            if (EntityManager.HasComponent<PlayerReady>(connection)) continue;
            if (!fogReady) continue;
            int network = EntityManager.GetComponentData<NetworkId>(connection).Value;
            if (EntityManager.HasComponent<PlayerBootstrapManifestState>(connection))
            {
                // A moving/dead/hidden gameplay ghost must not hold the loading barrier forever.
                if (!EntityManager.HasBuffer<RequiredBootstrapGhost>(connection)) continue;
                using var manifest = EntityManager.GetBuffer<RequiredBootstrapGhost>(connection, true).ToNativeArray(Allocator.Temp);
                for (int i = 0; i < manifest.Length; i++)
                {
                    var entry = manifest[i];
                    if (entry.Withdrawn) continue;
                    ghostLookup.TryGetValue(new SpawnedGhost { ghostId = entry.GhostId, spawnTick = entry.SpawnTick }, out var current);
                    if (current != Entity.Null && (EntityManager.HasComponent<PlayerContext>(current) || EntityManager.HasComponent<MatchSession>(current))) continue;
                    if (current != Entity.Null && (!EntityManager.HasComponent<CustomGhostRelevancy>(current)
                        || GameplayGhostVisibility.CanReceive(EntityManager, current, player.PlayerId, network, grid, fog, true))) continue;
                    entry.Withdrawn = true;
                    var currentManifest = EntityManager.GetBuffer<RequiredBootstrapGhost>(connection);
                    currentManifest[i] = entry;
                }
                PlayerBootstrapManifest.Drain(EntityManager, connection, ref player);
                continue;
            }
            using var required = new NativeList<RequiredBootstrapGhost>(Allocator.Temp);
            bool pending = false, contextFound = false, matchFound = false;
            foreach (var entity in ghosts)
            {
                bool context = EntityManager.HasComponent<PlayerContext>(entity);
                bool match = EntityManager.HasComponent<MatchSession>(entity);
                bool gameplay = EntityManager.HasComponent<UnitComponent>(entity)
                    || EntityManager.HasComponent<BuildingComponent>(entity) || EntityManager.HasComponent<ResourceNodeData>(entity);
                if (!context && !match && !gameplay) continue;
                bool relevant = !EntityManager.HasComponent<CustomGhostRelevancy>(entity)
                    || GameplayGhostVisibility.CanReceive(EntityManager, entity, player.PlayerId, network, grid, fog, true);
                if (!relevant) continue;
                contextFound |= context; matchFound |= match;
                var ghost = EntityManager.GetComponentData<GhostInstance>(entity);
                if (ghost.ghostId == 0) { pending = true; break; }
                required.Add(new RequiredBootstrapGhost { GhostId = ghost.ghostId, SpawnTick = ghost.spawnTick });
            }
            if (pending || !contextFound || !matchFound) continue;
            if (!EntityManager.HasBuffer<RequiredBootstrapGhost>(connection)) EntityManager.AddBuffer<RequiredBootstrapGhost>(connection);
            var manifestBuffer = EntityManager.GetBuffer<RequiredBootstrapGhost>(connection);
            manifestBuffer.Clear(); manifestBuffer.AddRange(required.AsArray());
            EntityManager.AddComponentData(connection, new PlayerBootstrapManifestState());
            PlayerBootstrapManifest.Drain(EntityManager, connection, ref player);
        }

        using var requests = EntityManager.CreateEntityQuery(typeof(PlayerReadyRequest), typeof(ReceiveRpcCommandRequest));
        using var entities = requests.ToEntityArray(Allocator.Temp);
        foreach (var request in entities)
        {
            var ready = EntityManager.GetComponentData<PlayerReadyRequest>(request);
            var connection = EntityManager.GetComponentData<ReceiveRpcCommandRequest>(request).SourceConnection;
            if (EntityManager.Exists(connection) && EntityManager.HasComponent<PlayerConnection>(connection)
                && !EntityManager.HasComponent<NetworkStreamRequestDisconnect>(connection))
            {
                var player = EntityManager.GetComponentData<PlayerConnection>(connection);
                if (PlayerSessionSlots.IsCurrent(EntityManager, connection, player) && player.BootstrapSent && ready.MatchSessionId == player.MatchSessionId
                    && ready.JoinGeneration == player.JoinGeneration && ready.Epoch == player.Epoch && ready.Epoch == epoch)
                {
                    var value = new PlayerReady { MatchSessionId = ready.MatchSessionId, JoinGeneration = ready.JoinGeneration, Epoch = ready.Epoch };
                    if (EntityManager.HasComponent<PlayerReady>(connection)) EntityManager.SetComponentData(connection, value);
                    else EntityManager.AddComponentData(connection, value);
                    PlayerSessionSlots.RecordProgress(EntityManager, connection, PlayerLoadingProgress.Ready, PlayerProtocolClock.Now(EntityManager));
                    PlayerNetwork.Send(EntityManager, connection, new PlayerReadyAcceptedRpc
                    { MatchSessionId = ready.MatchSessionId, JoinGeneration = ready.JoinGeneration, Epoch = ready.Epoch });
                }
            }
            EntityManager.DestroyEntity(request);
        }
    }
}

public static class PlayerBootstrapManifest
{
    public const int MaxMessagesPerUpdate = 128;

    public static void Drain(EntityManager em, Entity connection, ref PlayerConnection player)
    {
        var sending = em.GetComponentData<PlayerBootstrapManifestState>(connection);
        int count = em.GetBuffer<RequiredBootstrapGhost>(connection, true).Length;
        var version = player;
        int budget = MaxMessagesPerUpdate;
        while (sending.Cursor < count && budget > 0)
        {
            var entry = em.GetBuffer<RequiredBootstrapGhost>(connection, true)[sending.Cursor];
            Send(entry);
            if (entry.Withdrawn)
            {
                entry.WithdrawalSent = true;
                var manifest = em.GetBuffer<RequiredBootstrapGhost>(connection);
                manifest[sending.Cursor] = entry;
            }
            sending.Cursor++; budget--;
        }
        for (int i = 0; i < sending.Cursor && budget > 0; i++)
        {
            var entry = em.GetBuffer<RequiredBootstrapGhost>(connection, true)[i];
            if (!entry.Withdrawn || entry.WithdrawalSent) continue;
            Send(entry); entry.WithdrawalSent = true;
            var manifest = em.GetBuffer<RequiredBootstrapGhost>(connection);
            manifest[i] = entry; budget--;
        }
        em.SetComponentData(connection, sending);
        if (budget < MaxMessagesPerUpdate)
            PlayerSessionSlots.RecordProgress(em, connection, PlayerLoadingProgress.ManifestSending, PlayerProtocolClock.Now(em));
        if (!player.BootstrapSent && sending.Cursor == count && budget > 0)
        {
            PlayerNetwork.Send(em, connection, new PlayerBootstrapSummaryRpc
            { MatchSessionId = player.MatchSessionId, JoinGeneration = player.JoinGeneration, Epoch = player.Epoch, Count = count });
            player.BootstrapSent = true; em.SetComponentData(connection, player);
            PlayerSessionSlots.RecordProgress(em, connection, PlayerLoadingProgress.AwaitingClient, PlayerProtocolClock.Now(em));
        }

        void Send(RequiredBootstrapGhost entry) => PlayerNetwork.Send(em, connection, new PlayerBootstrapGhostRpc
        { MatchSessionId = version.MatchSessionId, JoinGeneration = version.JoinGeneration, Epoch = version.Epoch,
            GhostId = entry.GhostId, SpawnTick = entry.SpawnTick, Withdrawn = entry.Withdrawn });
    }
}
