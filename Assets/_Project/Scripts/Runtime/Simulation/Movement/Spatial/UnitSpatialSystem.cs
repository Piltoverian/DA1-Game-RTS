using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct UnitSpatialBucket : IComponentData
{
    public NativeParallelMultiHashMap<int, Entity> Bucket;
}

[UpdateAfter(typeof(SelectableSpatialSystem))]
[UpdateAfter(typeof(MovementAgentActuatorSystem))]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial struct UnitSpatialSystem : ISystem
{
    private ComponentLookup<EntityHealth> healthLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<UnitSpatialBucket>())
        {
            var bucket = new NativeParallelMultiHashMap<int, Entity>(10000, Allocator.Persistent);
            state.EntityManager.CreateSingleton(new UnitSpatialBucket { Bucket = bucket });
        }
        state.RequireForUpdate<GridComponent>();
        healthLookup = state.GetComponentLookup<EntityHealth>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        healthLookup.Update(ref state);

        var container = SystemAPI.GetSingletonRW<UnitSpatialBucket>();
        var grid = SystemAPI.GetSingleton<GridComponent>();
        var bucketMap = container.ValueRW.Bucket;

        bucketMap.Clear();
        if (!SpatialCellCoverage.IsGridValid(grid)) return;
        var footprints = SystemAPI.GetComponentLookup<BlockageData>(true);

        foreach (var (transform, unit, entity)
            in SystemAPI.Query<RefRO<LocalTransform>, RefRO<EntityOwner>>()
            .WithAll<EntityHealth>()
            .WithNone<Bullet, ArtilleryBullet>()
            .WithEntityAccess())
        {
            if (healthLookup.HasComponent(entity) && healthLookup[entity].CurrentHP <= 0f)
            {
                continue;
            }

            SpatialCellCoverage.Add(ref bucketMap, entity, transform.ValueRO.Position, grid, footprints);
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (SystemAPI.HasSingleton<UnitSpatialBucket>())
        {
            var container = SystemAPI.GetSingleton<UnitSpatialBucket>();
            if (container.Bucket.IsCreated)
            {
                container.Bucket.Dispose();
            }
        }
    }
}

// Use the same baked footprint and cell-edge inset as building placement and gathering.
internal static class SpatialCellCoverage
{
    public static bool IsGridValid(in GridComponent grid) =>
        grid.initialized && grid.width > 0 && grid.height > 0
        && (long)grid.width * grid.height <= int.MaxValue
        && math.isfinite(grid.cellsize) && grid.cellsize > 0 && math.all(math.isfinite(grid.origin));

    public static void Add(ref NativeParallelMultiHashMap<int, Entity> bucket, Entity entity,
        float3 position, in GridComponent grid, in ComponentLookup<BlockageData> footprints)
    {
        if (!math.all(math.isfinite(position))) return;
        if (footprints.HasComponent(entity))
        {
            if (!PlayerSpawnPlacement.TryFootprint(grid, position, footprints[entity].LocalRect,
                out var min, out var max)) return;
            for (int z = min.y; z <= max.y; z++)
            for (int x = min.x; x <= max.x; x++)
                bucket.Add(GridHelper.GetNodeIndex(new int2(x, z), grid), entity);
        }
        else
        {
            int2 cell = GridHelper.WorldToGrid(position, grid);
            if (PlayerSpawnPlacement.InBounds(cell, grid))
                bucket.Add(GridHelper.GetNodeIndex(cell, grid), entity);
        }
    }
}

