using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
[UpdateAfter(typeof(UnitMovementSystem))]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
partial struct BucketArrangeSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<BucketContainer>())
        {
            var bucket = new NativeParallelMultiHashMap<int, Entity>(10000, Allocator.Persistent);

            state.EntityManager.CreateSingleton(new BucketContainer
            {
                Bucket = bucket
            });
        }
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

            if (newIndex == oldIndex)
                continue;

            if (oldIndex >= 0)
                bucketMap.Remove(oldIndex, entity);

            bucketMap.Add(newIndex, entity);

            selectable.ValueRW.GridIndex = newIndex;
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (SystemAPI.HasSingleton<BucketContainer>())
        {
            var container = SystemAPI.GetSingleton<BucketContainer>();

            if (container.Bucket.IsCreated)
                container.Bucket.Dispose();
        }
    }
}
