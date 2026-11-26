using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>Bounded free-cell search around a migrated producer's authored exit.</summary>
public static class ProductionExitPlacement
{
    public static bool TryFind(EntityManager em, Entity producer, Entity prefab, float3 preferred,
        NativeList<float3> reserved, out float3 position)
    {
        position = preferred;
        using var grids = em.CreateEntityQuery(typeof(GridComponent), typeof(GridNodeCost));
        if (grids.CalculateEntityCount() != 1) return false;
        var gridEntity = grids.GetSingletonEntity();
        var grid = em.GetComponentData<GridComponent>(gridEntity);
        if (!grid.initialized || grid.cellsize <= 0) return false;
        var costs = em.GetBuffer<GridNodeCost>(gridEntity, true);
        if (costs.Length != grid.width * grid.height) return false;
        float radius = em.HasComponent<MovementAgentAvoidanceComponent>(prefab)
            ? em.GetComponentData<MovementAgentAvoidanceComponent>(prefab).radius : grid.cellsize * .4f;
        radius = math.max(radius, grid.cellsize * .25f);
        using var agents = em.CreateEntityQuery(typeof(MovementAgentComponent), typeof(LocalTransform));
        using var transforms = agents.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        int2 anchor = GridHelper.WorldToGrid(preferred, grid);
        float3 producerPosition = em.GetComponentData<LocalTransform>(producer).Position;
        bool hasFootprint = em.HasComponent<BlockageData>(producer);
        var footprint = hasFootprint ? em.GetComponentData<BlockageData>(producer).LocalRect : default;
        for (int ring = 0; ring <= 3; ring++)
        for (int z = -ring; z <= ring; z++)
        for (int x = -ring; x <= ring; x++)
        {
            if (math.max(math.abs(x), math.abs(z)) != ring) continue;
            var cell = anchor + new int2(x, z);
            if (!PlayerSpawnPlacement.InBounds(cell, grid)) continue;
            float3 candidate = GridHelper.GridToWorld(cell, grid); candidate.y = preferred.y;
            if (hasFootprint && math.all(candidate.xz + radius > producerPosition.xz + footprint.MinPoint) &&
                math.all(candidate.xz - radius < producerPosition.xz + footprint.MaxPoint)) continue;
            int2 lo = GridHelper.WorldToGrid(candidate - new float3(radius, 0, radius), grid);
            int2 hi = GridHelper.WorldToGrid(candidate + new float3(radius, 0, radius), grid);
            if (!PlayerSpawnPlacement.InBounds(lo, grid) || !PlayerSpawnPlacement.InBounds(hi, grid)) continue;
            bool free = true;
            for (int cy = lo.y; cy <= hi.y && free; cy++)
            for (int cx = lo.x; cx <= hi.x; cx++)
            {
                int cost = costs[GridHelper.GetNodeIndex(new int2(cx, cy), grid)].cost;
                if (cost >= 255 || cost == int.MaxValue) { free = false; break; }
            }
            if (!free) continue;
            float spacing = radius + grid.cellsize * .4f;
            float spacingSq = spacing * spacing;
            foreach (var agent in transforms)
                if (math.distancesq(candidate.xz, agent.Position.xz) < spacingSq) { free = false; break; }
            if (!free) continue;
            foreach (var other in reserved)
                if (math.distancesq(candidate.xz, other.xz) < spacingSq) { free = false; break; }
            if (!free) continue;
            position = candidate; reserved.Add(candidate); return true;
        }
        return false;
    }
}
