using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

public struct NeutralSpatialBucket : IComponentData
{
    public NativeParallelMultiHashMap<int, Entity> Bucket;
}

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(UnitSpatialSystem))]
[UpdateBefore(typeof(FogOfWarSpatialSystem))]
public partial struct NeutralSpatialSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<NeutralSpatialBucket>())
            state.EntityManager.CreateSingleton(new NeutralSpatialBucket
            {
                Bucket = new NativeParallelMultiHashMap<int, Entity>(10000, Allocator.Persistent)
            });
        state.RequireForUpdate<GridComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var bucket = SystemAPI.GetSingletonRW<NeutralSpatialBucket>().ValueRW.Bucket;
        bucket.Clear();
        var grid = SystemAPI.GetSingleton<GridComponent>();
        if (!SpatialCellCoverage.IsGridValid(grid)) return;
        var footprints = SystemAPI.GetComponentLookup<BlockageData>(true);
        var health = SystemAPI.GetComponentLookup<EntityHealth>(true);
        var resources = SystemAPI.GetComponentLookup<ResourceNodeData>(true);
        foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>()
            .WithAny<BlockageData, ResourceNodeData>()
            .WithNone<EntityOwner, ResourceNodePendingConfig>()
            .WithNone<Bullet, ArtilleryBullet>()
            .WithEntityAccess())
        {
            if (health.HasComponent(entity) && health[entity].CurrentHP <= 0) continue;
            if (resources.HasComponent(entity) && resources[entity].Amount <= 0) continue;
            SpatialCellCoverage.Add(ref bucket, entity, transform.ValueRO.Position, grid, footprints);
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (SystemAPI.TryGetSingleton<NeutralSpatialBucket>(out var data) && data.Bucket.IsCreated)
            data.Bucket.Dispose();
    }
}
