using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// [_SpatialGrid] Sub-layer: Unit Spatial Index
/// Cập nhật vị trí của các Unit có UnitAvoidanceComponent vào BucketMap (HashMap<cell, Entity>).
/// Tạo và sở hữu BucketContainer singleton. Chạy trước LocalAvoidance để neighbor lookup luôn mới nhất.
/// </summary>
[UpdateAfter(typeof(FlowDirectionSystem))]
[UpdateBefore(typeof(MovementAgentTargetSystem))]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial struct UnitSpatialSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        // Tạo singleton BucketContainer dùng chung với SelectableSpatialSystem và LocalAvoidanceSystem
        if (!SystemAPI.HasSingleton<BucketContainer>())
        {
            var bucket = new NativeParallelMultiHashMap<int, Entity>(10000, Allocator.Persistent);
            state.EntityManager.CreateSingleton(new BucketContainer { Bucket = bucket });
        }
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var container = SystemAPI.GetSingletonRW<BucketContainer>();
        var grid = SystemAPI.GetSingleton<GridComponent>();
        var bucketMap = container.ValueRW.Bucket;

        foreach (var (transform, avoidance, entity)
            in SystemAPI.Query<RefRO<LocalTransform>, RefRW<MovementAgentAvoidanceComponent>>()
            .WithEntityAccess())
        {
            float3 pos = transform.ValueRO.Position;

            int newIndex = GridHelper.GetNodeIndex(
                GridHelper.WorldToGrid(pos, grid),
                grid
            );

            int oldIndex = avoidance.ValueRO.gridIndex;
            if (newIndex == oldIndex) continue;

            if (oldIndex >= 0)
                bucketMap.Remove(oldIndex, entity);

            bucketMap.Add(newIndex, entity);
            avoidance.ValueRW.gridIndex = newIndex;
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        // UnitSpatialSystem là owner của BucketContainer → chịu trách nhiệm Dispose
        if (SystemAPI.HasSingleton<BucketContainer>())
        {
            var container = SystemAPI.GetSingleton<BucketContainer>();
            if (container.Bucket.IsCreated)
                container.Bucket.Dispose();
        }
    }
}
