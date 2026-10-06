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

        foreach (var (transform, unit, entity)
            in SystemAPI.Query<RefRO<LocalTransform>, RefRO<EntityOwner>>()
            .WithAll<EntityHealth>()
            .WithEntityAccess())
        {
            if (healthLookup.HasComponent(entity) && healthLookup[entity].CurrentHP <= 0f)
            {
                continue;
            }

            float3 pos = transform.ValueRO.Position;
            int newIndex = GridHelper.GetNodeIndex(
                GridHelper.WorldToGrid(pos, grid),
                grid
            );

            bucketMap.Add(newIndex, entity);
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

