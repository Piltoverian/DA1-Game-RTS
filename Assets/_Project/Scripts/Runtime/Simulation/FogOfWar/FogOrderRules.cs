using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

// Information captured at issue time; never refresh it from a hidden target.
public struct FogKnownTarget
{
    public bool HasValue;
    public FogRenderKind Kind;
    public float3 Position;
    public float2 Min, Max;
    public ResourceType ResourceType;
}

public struct FogTargetTravel : IComponentData
{
    public int PlayerId;
    public FogKnownTarget Known;
    public bool ApproachRequested;
}

public enum BuildingPreviewState { Unknown, Valid, Invalid }

public static class FogOrderRules
{
    public const int NearbyCellRadius = 5;

    public static BuildingPreviewState Preview(EntityManager em, int player, float3 position, StartEndRect rect)
    {
        if (!TryGrid(em, out var gridEntity, out var grid)
            || !PlayerSpawnPlacement.TryFootprint(grid, position, rect, out var min, out var max)
            || !em.HasBuffer<GridTerrain>(gridEntity)) return BuildingPreviewState.Invalid;
        using var fogQuery = em.CreateEntityQuery(typeof(FogOfWarSpatialSystemData));
        bool hasFog = !fogQuery.IsEmptyIgnoreFilter;
        var fog = fogQuery.CalculateEntityCount() == 1 ? fogQuery.GetSingleton<FogOfWarSpatialSystemData>() : default;
        var terrain = em.GetBuffer<GridTerrain>(gridEntity, true);
        if (terrain.Length != grid.width * grid.height) return BuildingPreviewState.Invalid;
        bool allVisible = true;
        long? level = null;
        for (int y = min.y; y <= max.y; y++)
        for (int x = min.x; x <= max.x; x++)
        {
            int index = y * grid.width + x;
            bool visible = !hasFog || fog.IsVisibleTo(index, player);
            allVisible &= visible;
            if (hasFog && !visible && !fog.IsExploredBy(index, player)) continue;
            var cell = terrain[index];
            if (!cell.walkable || cell.isMountain || cell.isCliff || cell.RampId >= 0
                || (cell.UsesVertexCorners && cell.CornerMask != 0 && cell.CornerMask != 15)) return BuildingPreviewState.Invalid;
            long height = (long)cell.heightLevel + (cell.UsesVertexCorners && cell.CornerMask == 15 ? 1 : 0);
            if (level.HasValue && level.Value != height) return BuildingPreviewState.Invalid;
            level = height;
        }
        if (!allVisible) return BuildingPreviewState.Unknown;
        if (!em.HasBuffer<GridNodeCost>(gridEntity)) return BuildingPreviewState.Invalid;
        using var buckets = em.CreateEntityQuery(typeof(MovementAgentBucket));
        var bucket = buckets.CalculateEntityCount() == 1 ? buckets.GetSingleton<MovementAgentBucket>().Bucket : default;
        return BuildingPlacementValidator.Validate(grid, terrain.AsNativeArray(), em.GetBuffer<GridNodeCost>(gridEntity, true).AsNativeArray(),
            position, rect, bucket, out _, out _) == BuildingPlacementFailure.None ? BuildingPreviewState.Valid : BuildingPreviewState.Invalid;
    }

    public static bool TryGrid(EntityManager em, out Entity entity, out GridComponent grid)
    {
        using var q = em.CreateEntityQuery(typeof(GridComponent));
        entity = q.CalculateEntityCount() == 1 ? q.GetSingletonEntity() : Entity.Null;
        grid = entity != Entity.Null ? em.GetComponentData<GridComponent>(entity) : default;
        return entity != Entity.Null && SpatialCellCoverage.IsGridValid(grid);
    }

    public static bool HasFog(EntityManager em)
    {
        using var q = em.CreateEntityQuery(typeof(FogOfWarSpatialSystemData));
        return !q.IsEmptyIgnoreFilter;
    }

    public static bool CellsVisible(EntityManager em, int player, int2 min, int2 max, bool all)
    {
        using var q = em.CreateEntityQuery(typeof(FogOfWarSpatialSystemData));
        if (q.IsEmptyIgnoreFilter) return true; // Existing fixtures/maps without FOW.
        if (q.CalculateEntityCount() != 1 || !TryGrid(em, out _, out var grid)) return false;
        var fog = q.GetSingleton<FogOfWarSpatialSystemData>();
        for (int y = min.y; y <= max.y; y++)
        for (int x = min.x; x <= max.x; x++)
        {
            bool visible = PlayerSpawnPlacement.InBounds(new int2(x, y), grid) && fog.IsVisibleTo(y * grid.width + x, player);
            if (visible && !all) return true;
            if (!visible && all) return false;
        }
        return all;
    }

    public static bool IsVisible(EntityManager em, int player, Entity entity)
    {
        if (!em.Exists(entity) || !em.HasComponent<LocalTransform>(entity)) return false;
        if (!HasFog(em)) return true;
        if (!TryGrid(em, out _, out var grid)) return false;
        var pos = em.GetComponentData<LocalTransform>(entity).Position;
        int2 min = GridHelper.WorldToGrid(pos, grid), max = min;
        if (em.HasComponent<BlockageData>(entity) && !PlayerSpawnPlacement.TryFootprint(grid, pos,
            em.GetComponentData<BlockageData>(entity).LocalRect, out min, out max)) return false;
        return CellsVisible(em, player, min, max, false);
    }

    public static bool KnownAreaVisible(EntityManager em, int player, in FogKnownTarget known)
    {
        if (!TryGrid(em, out _, out var grid)) return !HasFog(em);
        var min = GridHelper.WorldToGrid(new float3(known.Min.x, 0, known.Min.y), grid);
        var max = GridHelper.WorldToGrid(new float3(known.Max.x - .001f, 0, known.Max.y - .001f), grid);
        return CellsVisible(em, player, min, max, false);
    }

    public static void BeginTravel(EntityManager em, Entity worker, int player, in FogKnownTarget known)
    {
        var travel = new FogTargetTravel { PlayerId = player, Known = known };
        if (em.HasComponent<FogTargetTravel>(worker)) em.SetComponentData(worker, travel);
        else em.AddComponentData(worker, travel);
        if (em.HasComponent<TargetCache>(worker)) em.SetComponentData(worker,
            new TargetCache { lastTargetPosition = known.Position });
    }

    public static bool Approach(EntityManager em, Entity worker, float3 pos, float2 min, float2 max, float range)
    {
        float2 closest = math.clamp(pos.xz, min, max);
        if (math.distancesq(pos.xz, closest) <= range * range) return true;
        if (!em.HasComponent<MoveOverride>(worker)) return false;
        float radius = em.HasComponent<MovementAgentAvoidanceComponent>(worker)
            ? math.max(0, em.GetComponentData<MovementAgentAvoidanceComponent>(worker).radius) : 0;
        float2 goal = closest + math.normalizesafe(pos.xz - closest) * (radius + .01f);
        var move = em.GetComponentData<MoveOverride>(worker);
        var target = new float3(goal.x, pos.y, goal.y);
        if (!em.IsComponentEnabled<MoveOverride>(worker) || math.distancesq(move.targetPosition, target) > .001f)
        {
            move.targetPosition = target; move.targetApplied = false;
            em.SetComponentData(worker, move); em.SetComponentEnabled<MoveOverride>(worker, true);
        }
        return false;
    }

    public static void StopMove(EntityManager em, Entity worker)
    {
        if (em.HasComponent<MoveOverride>(worker)) em.SetComponentEnabled<MoveOverride>(worker, false);
        if (em.HasComponent<MovementAgentComponent>(worker))
        {
            var agent = em.GetComponentData<MovementAgentComponent>(worker);
            agent.hastarget = false; agent.velocity = float3.zero; em.SetComponentData(worker, agent);
        }
        if (em.HasComponent<MovementSteeringComponent>(worker))
        {
            var steering = em.GetComponentData<MovementSteeringComponent>(worker);
            steering.isSettled = true; steering.stuckTime = 0; em.SetComponentData(worker, steering);
        }
    }

    // Same search window and free perimeter island rule as resource gathering.
    public static bool IsNearbyReachable(EntityManager em, Entity worker, Entity target, float3 center, in GridComponent grid, Entity gridEntity)
    {
        if (!em.HasComponent<LocalTransform>(target) || !em.HasBuffer<GridIsland>(gridEntity)) return false;
        var position = em.GetComponentData<LocalTransform>(target).Position;
        var targetCenter = em.HasComponent<BlockageData>(target) ? em.GetComponentData<BlockageData>(target).GetWorldCenter(position) : position;
        int2 delta = GridHelper.WorldToGrid(targetCenter, grid) - GridHelper.WorldToGrid(center, grid);
        if (math.any(math.abs(delta) > NearbyCellRadius)) return false;
        var islands = em.GetBuffer<GridIsland>(gridEntity, true);
        int2 cell = GridHelper.WorldToGrid(em.GetComponentData<LocalTransform>(worker).Position, grid);
        if (!PlayerSpawnPlacement.InBounds(cell, grid) || islands.Length != grid.width * grid.height) return false;
        int island = islands[cell.y * grid.width + cell.x].islandID;
        if (island <= 0) return false;
        if (!em.HasComponent<BlockageData>(target))
        {
            cell = GridHelper.WorldToGrid(position, grid);
            return PlayerSpawnPlacement.InBounds(cell, grid) && islands[cell.y * grid.width + cell.x].islandID == island;
        }
        if (!PlayerSpawnPlacement.TryFootprint(grid, position, em.GetComponentData<BlockageData>(target).LocalRect, out var min, out var max)) return false;
        for (int y = min.y - 1; y <= max.y + 1; y++)
        for (int x = min.x - 1; x <= max.x + 1; x++)
        {
            bool side = ((y == min.y - 1 || y == max.y + 1) && x >= min.x && x <= max.x)
                || ((x == min.x - 1 || x == max.x + 1) && y >= min.y && y <= max.y);
            if (side && PlayerSpawnPlacement.InBounds(new int2(x, y), grid) && islands[y * grid.width + x].islandID == island) return true;
        }
        return false;
    }
}

[UpdateAfter(typeof(CommandQueue))]
[UpdateBefore(typeof(BuilderSystem))]
[UpdateBefore(typeof(WorkerGatherSystem))]
public partial struct FogTargetTravelSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;
        using var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (travel, transform, entity) in SystemAPI.Query<RefRW<FogTargetTravel>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            var known = travel.ValueRO.Known;
            bool visible = FogOrderRules.KnownAreaVisible(em, travel.ValueRO.PlayerId, known);
            bool failed = !visible && travel.ValueRO.ApproachRequested
                && math.distancesq(transform.ValueRO.Position.xz, math.clamp(transform.ValueRO.Position.xz, known.Min, known.Max)) > .25f
                && (!em.HasComponent<MoveOverride>(entity) || !em.IsComponentEnabled<MoveOverride>(entity));
            if (visible || failed || FogOrderRules.Approach(em, entity, transform.ValueRO.Position, known.Min, known.Max, .5f))
            {
                if (failed && em.HasComponent<WorkerGatherData>(entity))
                {
                    var gather = em.GetComponentData<WorkerGatherData>(entity);
                    gather.TargetNode = Entity.Null; gather.KnownTarget = default;
                    gather.State = WorkerGatherState.GoingToNode; em.SetComponentData(entity, gather);
                }
                ecb.RemoveComponent<FogTargetTravel>(entity);
                FogOrderRules.StopMove(em, entity);
            }
            else travel.ValueRW.ApproachRequested = true;
        }
        ecb.Playback(em);
    }
}
