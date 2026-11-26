using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

// Order movement and reachability; visibility policy stays in FogOrderRules.
public static class KnownTargetOrders
{
    public const int NearbyCellRadius = 5;

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
        MovementAgentAPI.PauseAgent(em, worker);
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
