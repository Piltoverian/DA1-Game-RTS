using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(MovementAgentPathRequestSystem))]
[UpdateBefore(typeof(IntegrationFieldSystem))]
public partial struct FlowFieldAssignmentSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridComponent>();
        state.RequireForUpdate<FlowFieldCache>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var grid = SystemAPI.GetSingleton<GridComponent>();
        var cacheEntity = SystemAPI.GetSingletonEntity<FlowFieldCache>();
        var cacheBuffer = SystemAPI.GetBuffer<FlowFieldCacheEntry>(cacheEntity);
        var fixedFrameCount = SystemAPI.GetSingleton<FixedFrameCount>();
        
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (request, move, steering, entity) in 
                 SystemAPI.Query<RefRO<TargetChangeRequest>, RefRW<MovementAgentComponent>, RefRW<MovementSteeringComponent>>().WithEntityAccess())
        {
            int2 targetCell = GridHelper.WorldToGrid(request.ValueRO.newWorldTarget, grid);
            
            Entity newField = FlowFieldCacheHelper.TryGetFieldFromCache(ref cacheBuffer, targetCell, (uint)fixedFrameCount.value);
            
            if (newField == Entity.Null)
            {
                foreach (var (fieldData, fieldEt) in SystemAPI.Query<FlowField>().WithEntityAccess())
                {
                    if (math.all(fieldData.targetcell == targetCell))
                    {
                        newField = fieldEt;
                        break;
                    }
                }
            }

            if (newField == Entity.Null)
            {
                newField = FlowFieldCacheHelper.CreateFlowField(
                    ecb, 
                    request.ValueRO.newWorldTarget, 
                    (uint)fixedFrameCount.value, 
                    grid, 
                    state.EntityManager,
                    ref cacheBuffer, 
                    targetCell);
            }

            FlowFieldHelper.AssignFieldToMoveComponent(
                ref move.ValueRW, 
                ref steering.ValueRW, 
                newField, 
                request.ValueRO.newWorldTarget, // TRUYỀN TỌA ĐỘ ĐÍCH VÀO ĐÂY
                ecb, 
                state.EntityManager);

            // Reset trạng thái stuck khi nhận lệnh mới
            steering.ValueRW.stuckTime = 0;
            steering.ValueRW.lastPosition = SystemAPI.GetComponent<Unity.Transforms.LocalTransform>(entity).Position;

            ecb.RemoveComponent<TargetChangeRequest>(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
