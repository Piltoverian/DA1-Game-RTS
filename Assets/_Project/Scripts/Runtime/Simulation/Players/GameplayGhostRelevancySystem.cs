using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

public struct CustomGhostRelevancy : IComponentData { }

public static class GameplayGhostVisibility
{
    public static bool TryGetFog(EntityManager em, out GridComponent grid, out FogOfWarSpatialSystemData fog)
    {
        grid = default; fog = default;
        using var grids = em.CreateEntityQuery(typeof(GridComponent));
        using var fogs = em.CreateEntityQuery(typeof(FogOfWarSpatialSystemData));
        if (grids.CalculateEntityCount() != 1 || fogs.CalculateEntityCount() != 1) return false;
        var entity = grids.GetSingletonEntity(); grid = grids.GetSingleton<GridComponent>();
        fog = fogs.GetSingleton<FogOfWarSpatialSystemData>();
        uint revision = em.HasComponent<MapBuildState>(entity) ? em.GetComponentData<MapBuildState>(entity).Revision : 0;
        if (em.HasComponent<MapBuildState>(entity))
        {
            var phase = em.GetComponentData<MapBuildState>(entity).Phase;
            if (phase == MapBuildPhase.Requested || phase == MapBuildPhase.Failed) return false;
        }
        return fog.Ready && fog.SourceGrid == entity && fog.GridRevision == revision && grid.initialized
            && grid.width > 0 && grid.height > 0 && (long)grid.width * grid.height <= int.MaxValue
            && grid.cellsize > 0 && math.isfinite(grid.cellsize) && math.all(math.isfinite(grid.origin));
    }
    public static bool CanReceive(EntityManager em, Entity entity, int playerId, int networkId,
        in GridComponent grid, in FogOfWarSpatialSystemData fog, bool fogReady)
    {
        if (playerId < 0 || networkId <= 0 || !em.Exists(entity) || em.HasComponent<Prefab>(entity)) return false;
        if (em.HasComponent<TerrainChunkGhost>(entity))
            return em.HasComponent<GhostOwner>(entity) && em.GetComponentData<GhostOwner>(entity).NetworkId == networkId;
        if (em.HasComponent<PlayerContext>(entity)) return em.GetComponentData<PlayerContext>(entity).PlayerId == playerId;
        bool projectile = em.HasComponent<Bullet>(entity) || em.HasComponent<ArtilleryBullet>(entity);
        if (!projectile && em.HasComponent<EntityOwner>(entity)
            && em.GetComponentData<EntityOwner>(entity).PlayerID == playerId) return true;
        if (!fogReady || !fog.Visible.IsCreated || em.HasComponent<ResourceNodePendingConfig>(entity)
            || !em.HasComponent<LocalTransform>(entity)) return false;
        var position = em.GetComponentData<LocalTransform>(entity).Position;
        if (!math.all(math.isfinite(position))) return false;
        int2 min = GridHelper.WorldToGrid(position, grid), max = min;
        if (em.HasComponent<BlockageData>(entity) && !PlayerSpawnPlacement.TryFootprint(grid, position,
            em.GetComponentData<BlockageData>(entity).LocalRect, out min, out max)) return false;
        for (int y = min.y; y <= max.y; y++)
        for (int x = min.x; x <= max.x; x++)
            if (PlayerSpawnPlacement.InBounds(new int2(x, y), grid)
                && fog.IsVisibleTo(GridHelper.GetNodeIndex(new int2(x, y), grid), playerId)) return true;
        return false;
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(EndSimulationEntityCommandBufferSystem))]
[UpdateBefore(typeof(GhostSendSystem))]
public partial class GameplayGhostRelevancySystem : SystemBase
{
    EntityQuery customGhosts, publicGhosts, connections, grids, fogs;
    protected override void OnCreate()
    {
        RequireForUpdate<GhostRelevancy>();
        customGhosts = GetEntityQuery(typeof(GhostInstance), typeof(CustomGhostRelevancy));
        publicGhosts = GetEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<GhostInstance>() },
            None = new[] { ComponentType.ReadOnly<CustomGhostRelevancy>() }
        });
        connections = GetEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<NetworkId>(), ComponentType.ReadOnly<NetworkStreamInGame>() },
            None = new[] { ComponentType.ReadOnly<NetworkStreamRequestDisconnect>() }
        });
        grids = GetEntityQuery(typeof(GridComponent)); fogs = GetEntityQuery(typeof(FogOfWarSpatialSystemData));
    }

    protected override void OnUpdate()
    {
        EntityManager.CompleteAllTrackedJobs();
        var relevancy = SystemAPI.GetSingletonRW<GhostRelevancy>();
        // Ordinary ghosts keep their default delivery. Only tagged ghosts require explicit authorization.
        // An allow-list prevents the first snapshot leaking a newly assigned ghost ID inside GhostSendSystem.
        relevancy.ValueRW.GhostRelevancyMode = GhostRelevancyMode.SetIsRelevant;
        relevancy.ValueRW.DefaultRelevancyQuery = publicGhosts;
        var set = relevancy.ValueRW.GhostRelevancySet;
        set.Clear();
        GridComponent grid = default;
        FogOfWarSpatialSystemData fog = default;
        bool ready = false;
        if (grids.CalculateEntityCount() == 1 && fogs.CalculateEntityCount() == 1)
        {
            var ge = grids.GetSingletonEntity(); grid = grids.GetSingleton<GridComponent>();
            fog = fogs.GetSingleton<FogOfWarSpatialSystemData>();
            uint revision = EntityManager.HasComponent<MapBuildState>(ge) ? EntityManager.GetComponentData<MapBuildState>(ge).Revision : 0;
            ready = fog.Ready && fog.SourceGrid == ge && fog.GridRevision == revision && grid.initialized
                && grid.width > 0 && grid.height > 0 && (long)grid.width * grid.height <= int.MaxValue
                && grid.cellsize > 0 && math.isfinite(grid.cellsize) && math.all(math.isfinite(grid.origin));
            if (EntityManager.HasComponent<MapBuildState>(ge))
            {
                var phase = EntityManager.GetComponentData<MapBuildState>(ge).Phase;
                ready &= phase != MapBuildPhase.Requested && phase != MapBuildPhase.Failed;
            }
        }
        using var clients = connections.ToEntityArray(Allocator.Temp);
        using var ghosts = customGhosts.ToEntityArray(Allocator.Temp);
        foreach (var connection in clients)
        {
            if (!EntityManager.HasComponent<PlayerConnection>(connection)) continue;
            int player = EntityManager.GetComponentData<PlayerConnection>(connection).PlayerId;
            int network = EntityManager.GetComponentData<NetworkId>(connection).Value;
            foreach (var ghost in ghosts)
            {
                int id = EntityManager.GetComponentData<GhostInstance>(ghost).ghostId;
                if (id != 0 && GameplayGhostVisibility.CanReceive(EntityManager, ghost, player, network, grid, fog, ready))
                    set.TryAdd(new RelevantGhostForConnection(network, id), 1);
            }
        }
    }
}
