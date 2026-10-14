using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

// Gameplay acquisition is a circle on XZ, independent of collider/terrain height.
[UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(ShootAttackSystem))]
[BurstCompile]
public partial struct FindTargetSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridComponent>();
        state.RequireForUpdate<UnitSpatialBucket>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var grid = SystemAPI.GetSingleton<GridComponent>();
        if (grid.cellsize <= 0) return;
        var bucket = SystemAPI.GetSingleton<UnitSpatialBucket>().Bucket;
        var owners = SystemAPI.GetComponentLookup<EntityOwner>(true);
        var health = SystemAPI.GetComponentLookup<EntityHealth>(true);
        var transforms = SystemAPI.GetComponentLookup<LocalTransform>(true);
        using var contexts = SystemAPI.QueryBuilder().WithAll<PlayerContext>().Build()
            .ToComponentDataArray<PlayerContext>(Allocator.Temp);
        foreach (var (transform, find, target, owner) in SystemAPI.Query<RefRO<LocalTransform>,
            RefRW<FindTarget>, RefRW<Target>, RefRO<EntityOwner>>())
        {
            var current = target.ValueRO.targetEntity;
            if (current != Entity.Null && health.HasComponent(current) && health[current].CurrentHP > 0
                && owners.HasComponent(current) && !PlayerContextHelper.AreAllies(contexts, owners[current].PlayerID, owner.ValueRO.PlayerID)) continue;
            find.ValueRW.timer -= SystemAPI.Time.DeltaTime;
            if (find.ValueRO.timer > 0) continue;
            find.ValueRW.timer = find.ValueRO.timerMax;
            float range = math.max(0, find.ValueRO.range);
            float bestDistance = range * range;
            Entity best = Entity.Null;
            float2 position = transform.ValueRO.Position.xz;
            // UnitSpatialSystem publishes the bucket after actuation in the fixed tick.
            int2 center = GridHelper.WorldToGrid(transform.ValueRO.Position, grid);
            int extent = (int)math.ceil(range / grid.cellsize);
            int2 min = math.max(center - extent, 0);
            int2 max = math.min(center + extent, new int2(grid.width - 1, grid.height - 1));
            for (int z = min.y; z <= max.y; z++)
            for (int x = min.x; x <= max.x; x++) {
                if (!bucket.TryGetFirstValue(z * grid.width + x, out var candidate, out var iterator)) continue;
                do {
                    if (!health.HasComponent(candidate) || health[candidate].CurrentHP <= 0
                        || !owners.HasComponent(candidate) || PlayerContextHelper.AreAllies(contexts, owners[candidate].PlayerID, owner.ValueRO.PlayerID)
                        || !transforms.HasComponent(candidate)) continue;
                    float distance = math.distancesq(position, transforms[candidate].Position.xz);
                    if (distance <= bestDistance) { bestDistance = distance; best = candidate; }
                } while (bucket.TryGetNextValue(out candidate, ref iterator));
            }
            target.ValueRW.targetEntity = best;
        }
    }
}
