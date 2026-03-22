using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// [_SpatialGrid] Sub-layer: Selectable Spatial Index
/// Cập nhật vị trí của các Selectable entity vào BucketMap (HashMap<cell, Entity>).
/// Dùng chung BucketContainer singleton do UnitSpatialSystem tạo ra.
/// Chạy sau UnitSpatialSystem và trước SelectSystem.
/// </summary>
[UpdateAfter(typeof(UnitSpatialSystem))]
[UpdateBefore(typeof(SelectSystem))]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial struct SelectableSpatialSystem : ISystem
{
    // Đảm bảo BucketContainer đã được tạo bởi UnitSpatialSystem trước khi system này chạy
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BucketContainer>();
        state.RequireForUpdate<GridComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var container = SystemAPI.GetSingletonRW<BucketContainer>();
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
}
