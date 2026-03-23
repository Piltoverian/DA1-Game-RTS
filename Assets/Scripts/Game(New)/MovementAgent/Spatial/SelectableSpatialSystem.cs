using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// [_SpatialGrid] Sub-layer: Selectable Spatial Index
/// Cập nhật vị trí của các Selectable entity vào SelectableBucketContainer (HashMap<cell, Entity>).
/// Hệ thống này sở hữu SelectableBucketContainer singleton riêng, tách biệt với UnitSpatialSystem.
/// Chạy sau UnitSpatialSystem và trước SelectSystem.
/// </summary>
[UpdateAfter(typeof(UnitSpatialSystem))]
[UpdateBefore(typeof(SelectSystem))]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial struct SelectableSpatialSystem : ISystem
{
    // Đảm bảo Bucket đã được khởi tạo
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
       if (!SystemAPI.HasSingleton<SelectableBucketContainer>())
        {
            var bucket = new NativeParallelMultiHashMap<int, Entity>(10000, Allocator.Persistent);
            state.EntityManager.CreateSingleton(new SelectableBucketContainer { Bucket = bucket });
        }
        state.RequireForUpdate<GridComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var container = SystemAPI.GetSingletonRW<SelectableBucketContainer>();
        var grid = SystemAPI.GetSingleton<GridComponent>();
        var bucketMap = container.ValueRW.Bucket;

        foreach (var (transform, selectable, entity)
            in SystemAPI.Query<RefRO<LocalTransform>, RefRW<Selectable>>()
            .WithEntityAccess())
        {
            float3 pos = transform.ValueRO.Position;

            int newIndex = GridHelper.GetNodeIndex(
                GridHelper.WorldToGrid(pos, grid),
                grid
            );

            int oldIndex = selectable.ValueRO.GridIndex;
            if (newIndex == oldIndex) continue;

            if (oldIndex >= 0)
                bucketMap.Remove(oldIndex, entity);

            bucketMap.Add(newIndex, entity);
            selectable.ValueRW.GridIndex = newIndex;
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (SystemAPI.HasSingleton<SelectableBucketContainer>())
        {
            var container = SystemAPI.GetSingleton<SelectableBucketContainer>();
            if (container.Bucket.IsCreated)
                container.Bucket.Dispose();
        }
    }
}
