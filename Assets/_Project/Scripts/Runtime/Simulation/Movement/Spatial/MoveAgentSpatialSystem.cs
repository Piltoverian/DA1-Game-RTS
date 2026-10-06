using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(FlowDirectionSystem))]
[UpdateBefore(typeof(MovementAgentTargetSystem))]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial struct MoveAgentSpatialSystem : ISystem
{

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<MovementAgentBucket>())
        {
            var bucket = new NativeParallelMultiHashMap<int, Entity>(10000, Allocator.Persistent);
            state.EntityManager.CreateSingleton(new MovementAgentBucket { Bucket = bucket });
        }
        state.RequireForUpdate<GridComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var container = SystemAPI.GetSingletonRW<MovementAgentBucket>();
        var grid = SystemAPI.GetSingleton<GridComponent>();
        var bucketMap = container.ValueRW.Bucket;

        bucketMap.Clear();

        foreach (var (transform, avoidance, entity)
            in SystemAPI.Query<RefRO<LocalTransform>, RefRW<MovementAgentAvoidanceComponent>>()
            .WithEntityAccess())
        {
            float3 pos = transform.ValueRO.Position;

            int newIndex = GridHelper.GetNodeIndex(
                GridHelper.WorldToGrid(pos, grid),
                grid
            );

            bucketMap.Add(newIndex, entity);
            avoidance.ValueRW.gridIndex = newIndex; 
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (SystemAPI.HasSingleton<MovementAgentBucket>())
        {
            var container = SystemAPI.GetSingleton<MovementAgentBucket>();
            if (container.Bucket.IsCreated)
                container.Bucket.Dispose();
        }
    }
}
