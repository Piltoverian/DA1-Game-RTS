using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Physics;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class PlayerHandshakeReceiveSystem : SystemBase
{
    protected override void OnUpdate()
    {
        using var joins = EntityManager.CreateEntityQuery(typeof(PlayerJoinAcceptedRpc), typeof(ReceiveRpcCommandRequest));
        using var joinEntities = joins.ToEntityArray(Allocator.Temp);
        foreach (var request in joinEntities)
        {
            var value = EntityManager.GetComponentData<PlayerJoinAcceptedRpc>(request);
            var connection = EntityManager.GetComponentData<ReceiveRpcCommandRequest>(request).SourceConnection;
            if (EntityManager.Exists(connection) && EntityManager.HasComponent<NetworkId>(connection)
                && !EntityManager.HasComponent<NetworkStreamRequestDisconnect>(connection))
            {
                if (value.PlayerId < 0 || value.MatchSessionId == 0 || value.JoinGeneration == 0 || value.Epoch == 0)
                { EntityManager.DestroyEntity(request); continue; }
                if (EntityManager.HasComponent<ClientPlayerState>(connection)
                    && PlayerNetwork.CompareVersion(EntityManager.GetComponentData<ClientPlayerState>(connection),
                        value.MatchSessionId, value.JoinGeneration, value.Epoch) < 0)
                { EntityManager.DestroyEntity(request); continue; }
                bool same = EntityManager.HasComponent<ClientPlayerState>(connection)
                    && PlayerNetwork.Matches(EntityManager.GetComponentData<ClientPlayerState>(connection), value.MatchSessionId, value.JoinGeneration, value.Epoch);
                if (!same)
                {
                    var state = new ClientPlayerState { PlayerId = value.PlayerId, MatchSessionId = value.MatchSessionId,
                        JoinGeneration = value.JoinGeneration, Epoch = value.Epoch, ExpectedGhosts = -1 };
                    if (EntityManager.HasComponent<ClientPlayerState>(connection)) EntityManager.SetComponentData(connection, state);
                    else EntityManager.AddComponentData(connection, state);
                    if (!EntityManager.HasBuffer<RequiredBootstrapGhost>(connection)) EntityManager.AddBuffer<RequiredBootstrapGhost>(connection);
                    else EntityManager.GetBuffer<RequiredBootstrapGhost>(connection).Clear();
                    if (!EntityManager.HasBuffer<ObservedBootstrapGhost>(connection)) EntityManager.AddBuffer<ObservedBootstrapGhost>(connection);
                    else EntityManager.GetBuffer<ObservedBootstrapGhost>(connection).Clear();
                }
            }
            EntityManager.DestroyEntity(request);
        }
        using var failures = EntityManager.CreateEntityQuery(typeof(PlayerJoinRejectedRpc), typeof(ReceiveRpcCommandRequest));
        using var failureEntities = failures.ToEntityArray(Allocator.Temp);
        foreach (var request in failureEntities)
        {
            var connection = EntityManager.GetComponentData<ReceiveRpcCommandRequest>(request).SourceConnection;
            if (EntityManager.Exists(connection) && EntityManager.HasComponent<NetworkId>(connection))
            {
                var failure = new ClientJoinFailure { Reason = EntityManager.GetComponentData<PlayerJoinRejectedRpc>(request).Reason };
                if (EntityManager.HasComponent<ClientJoinFailure>(connection)) EntityManager.SetComponentData(connection, failure);
                else EntityManager.AddComponentData(connection, failure);
                if (!EntityManager.HasComponent<NetworkStreamRequestDisconnect>(connection)) EntityManager.AddComponent<NetworkStreamRequestDisconnect>(connection);
            }
            EntityManager.DestroyEntity(request);
        }
        if (!PlayerNetwork.TryLocal(World, out var current, out var player, false)) return;
        using var tickets = EntityManager.CreateEntityQuery(typeof(PlayerResumeTicketRpc), typeof(ReceiveRpcCommandRequest));
        using var ticketEntities = tickets.ToEntityArray(Allocator.Temp);
        foreach (var request in ticketEntities)
        {
            var value = EntityManager.GetComponentData<PlayerResumeTicketRpc>(request);
            if (EntityManager.GetComponentData<ReceiveRpcCommandRequest>(request).SourceConnection == current)
            {
                if (value.MatchSessionId > player.MatchSessionId || value.MatchSessionId == player.MatchSessionId && value.JoinGeneration > player.JoinGeneration) continue;
                if (value.MatchSessionId == player.MatchSessionId && value.JoinGeneration == player.JoinGeneration)
                {
                    using var endpoint = EntityManager.CreateEntityQuery(typeof(ClientConnectionEndpoint));
                    if (endpoint.CalculateEntityCount() == 1)
                        PlayerResumeStore.Save(PlayerResumeStore.Endpoint(EntityManager, current), value.MatchSessionId, value.Ticket, player.PlayerId,
                            endpoint.GetSingleton<ClientConnectionEndpoint>().Profile.ToString());
                }
            }
            EntityManager.DestroyEntity(request);
        }
        using var entries = EntityManager.CreateEntityQuery(typeof(PlayerBootstrapGhostRpc), typeof(ReceiveRpcCommandRequest));
        using var entryEntities = entries.ToEntityArray(Allocator.Temp);
        if (!EntityManager.HasBuffer<RequiredBootstrapGhost>(current)) EntityManager.AddBuffer<RequiredBootstrapGhost>(current);
        var requiredIndices = new Dictionary<SpawnedGhost, int>();
        var existing = EntityManager.GetBuffer<RequiredBootstrapGhost>(current, true);
        for (int i = 0; i < existing.Length; i++)
            requiredIndices[new SpawnedGhost { ghostId = existing[i].GhostId, spawnTick = existing[i].SpawnTick }] = i;
        foreach (var request in entryEntities)
        {
            var value = EntityManager.GetComponentData<PlayerBootstrapGhostRpc>(request);
            if (EntityManager.GetComponentData<ReceiveRpcCommandRequest>(request).SourceConnection != current)
            { EntityManager.DestroyEntity(request); continue; }
            if (PlayerNetwork.CompareVersion(player, value.MatchSessionId, value.JoinGeneration, value.Epoch) > 0) continue;
            if (PlayerNetwork.Matches(player, value.MatchSessionId, value.JoinGeneration, value.Epoch))
            {
                var list = EntityManager.GetBuffer<RequiredBootstrapGhost>(current);
                var key = new SpawnedGhost { ghostId = value.GhostId, spawnTick = value.SpawnTick };
                if (requiredIndices.TryGetValue(key, out int index))
                { var entry = list[index]; entry.Withdrawn |= value.Withdrawn; list[index] = entry; }
                else
                {
                    requiredIndices.Add(key, list.Length);
                    list.Add(new RequiredBootstrapGhost { GhostId = value.GhostId, SpawnTick = value.SpawnTick, Withdrawn = value.Withdrawn });
                }
            }
            EntityManager.DestroyEntity(request);
        }
        using var summaries = EntityManager.CreateEntityQuery(typeof(PlayerBootstrapSummaryRpc), typeof(ReceiveRpcCommandRequest));
        using var summaryEntities = summaries.ToEntityArray(Allocator.Temp);
        foreach (var request in summaryEntities)
        {
            var value = EntityManager.GetComponentData<PlayerBootstrapSummaryRpc>(request);
            if (EntityManager.GetComponentData<ReceiveRpcCommandRequest>(request).SourceConnection != current)
            { EntityManager.DestroyEntity(request); continue; }
            if (PlayerNetwork.CompareVersion(player, value.MatchSessionId, value.JoinGeneration, value.Epoch) > 0) continue;
            if (PlayerNetwork.Matches(player, value.MatchSessionId, value.JoinGeneration, value.Epoch) && value.Count > 0)
                player.ExpectedGhosts = value.Count;
            EntityManager.DestroyEntity(request);
        }
        using var accepted = EntityManager.CreateEntityQuery(typeof(PlayerReadyAcceptedRpc), typeof(ReceiveRpcCommandRequest));
        using var acceptedEntities = accepted.ToEntityArray(Allocator.Temp);
        foreach (var request in acceptedEntities)
        {
            var value = EntityManager.GetComponentData<PlayerReadyAcceptedRpc>(request);
            if (EntityManager.GetComponentData<ReceiveRpcCommandRequest>(request).SourceConnection != current)
            { EntityManager.DestroyEntity(request); continue; }
            if (PlayerNetwork.CompareVersion(player, value.MatchSessionId, value.JoinGeneration, value.Epoch) > 0) continue;
            if (PlayerNetwork.Matches(player, value.MatchSessionId, value.JoinGeneration, value.Epoch))
                player.Phase = ClientLoadingPhase.ServerReady;
            EntityManager.DestroyEntity(request);
        }
        EntityManager.SetComponentData(current, player);
    }
}

public static class PlayerBootstrapReceipt
{
    public static void Observe(EntityManager em, Entity connection)
    {
        if (!em.HasBuffer<ObservedBootstrapGhost>(connection)) em.AddBuffer<ObservedBootstrapGhost>(connection);
        using var query = em.CreateEntityQuery(typeof(GhostInstance));
        using var ghosts = query.ToComponentDataArray<GhostInstance>(Allocator.Temp);
        var observed = em.GetBuffer<ObservedBootstrapGhost>(connection);
        var keys = new HashSet<SpawnedGhost>();
        foreach (var old in observed) keys.Add(new SpawnedGhost { ghostId = old.GhostId, spawnTick = old.SpawnTick });
        foreach (var ghost in ghosts)
        {
            if (ghost.ghostId == 0) continue;
            if (keys.Add(new SpawnedGhost { ghostId = ghost.ghostId, spawnTick = ghost.spawnTick }))
                observed.Add(new ObservedBootstrapGhost { GhostId = ghost.ghostId, SpawnTick = ghost.spawnTick });
        }
        var required = em.GetBuffer<RequiredBootstrapGhost>(connection);
        for (int i = 0; i < required.Length; i++)
        {
            var entry = required[i];
            if (entry.Received || entry.Withdrawn) continue;
            if (keys.Contains(new SpawnedGhost { ghostId = entry.GhostId, spawnTick = entry.SpawnTick }))
            { entry.Received = true; required[i] = entry; }
        }
    }

    public static bool Complete(DynamicBuffer<RequiredBootstrapGhost> required, int expected)
    {
        if (expected <= 0 || required.Length != expected) return false;
        foreach (var ghost in required) if (!ghost.Received && !ghost.Withdrawn) return false;
        return true;
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(PresentationSystemGroup), OrderLast = true)]
public partial class PlayerHandshakeClientSystem : SystemBase
{
    protected override void OnUpdate()
    {
        if (!PlayerNetwork.TryLocal(World, out var connection, out var player, false)
            || player.Phase != ClientLoadingPhase.Receiving) return;
        PlayerBootstrapReceipt.Observe(EntityManager, connection);
        var receiver = World.GetExistingSystemManaged<TerrainNetworkReceiveSystem>();
        if (receiver == null || !receiver.View.Ready || receiver.Header.Session != player.MatchSessionId
            || receiver.Header.JoinGeneration != player.JoinGeneration || receiver.Header.Epoch != player.Epoch) return;
        if (player.InitialTerrainVersion == 0)
        {
            player.InitialTerrainVersion = receiver.View.GeometryVersion;
            EntityManager.SetComponentData(connection, player);
        }
        if (player.ExpectedGhosts <= 0) return;
        if (!ClientPresentationCatalog.IsReady(EntityManager) || !NeutralMemoryClientSystem.IsReady(World, player)) return;
        using var presentation = EntityManager.CreateEntityQuery(typeof(MapPresentationBinding), typeof(MapPresentationState));
        using var presentations = presentation.ToEntityArray(Allocator.Temp);
        bool applied = false;
        foreach (var entity in presentations)
        {
            var binding = EntityManager.GetComponentObject<MapPresentationBinding>(entity);
            if (!binding.Owner || !binding.Owner.isActiveAndEnabled) continue;
            var state = EntityManager.GetComponentData<MapPresentationState>(entity);
            if (state.Phase != MapPresentationPhase.Ready || state.Revision < player.InitialTerrainVersion
                || !binding.Renderer || binding.View != receiver.View) return;
            applied = true;
        }
        if (!applied) return;
        if (!PlayerBootstrapReceipt.Complete(EntityManager.GetBuffer<RequiredBootstrapGhost>(connection, true), player.ExpectedGhosts)) return;
        if (PlayerContextHelper.GetPlayerContextEntity(EntityManager, player.PlayerId, out var context, out _) != FunctionResult.Success
            || !EntityManager.HasComponent<PlayerContextSession>(context) || !EntityManager.HasComponent<GhostOwner>(context)) return;
        var contextSession = EntityManager.GetComponentData<PlayerContextSession>(context);
        if (contextSession.MatchSessionId != player.MatchSessionId || contextSession.JoinGeneration != player.JoinGeneration
            || EntityManager.GetComponentData<GhostOwner>(context).NetworkId != EntityManager.GetComponentData<NetworkId>(connection).Value) return;
        using var matchQuery = EntityManager.CreateEntityQuery(typeof(MatchSession));
        if (!matchQuery.TryGetSingleton<MatchSession>(out var matchSession) || matchSession.Id != player.MatchSessionId) return;
        using var physics = EntityManager.CreateEntityQuery(typeof(PhysicsWorldSingleton));
        using var selection = EntityManager.CreateEntityQuery(typeof(DOTSSelectManagerComponent), typeof(SelectionRequest));
        if (physics.CalculateEntityCount() != 1 || selection.CalculateEntityCount() != 1) return;
        var value = new PlayerReadyRequest { MatchSessionId = player.MatchSessionId, JoinGeneration = player.JoinGeneration, Epoch = player.Epoch };
        player.Phase = ClientLoadingPhase.ReadySent;
        EntityManager.SetComponentData(connection, player);
        PlayerNetwork.Send(EntityManager, connection, value);
    }
}
