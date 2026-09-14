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
        var refCountDeltas = new NativeHashMap<Entity, int>(16, Allocator.Temp);
        var newFields = new NativeHashSet<Entity>(16, Allocator.Temp);

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
                
                newFields.Add(newField);
            }

            FlowFieldHelper.AssignFieldToMoveComponent(
                ref move.ValueRW, 
                ref steering.ValueRW, 
                newField, 
                request.ValueRO.newWorldTarget, // TRUYỀN TỌA ĐỘ ĐÍCH VÀO ĐÂY
                entity,
                ecb, 
                state.EntityManager,
                ref refCountDeltas);

            // Reset trạng thái stuck khi nhận lệnh mới
            steering.ValueRW.stuckTime = 0;
            steering.ValueRW.lastPosition = SystemAPI.GetComponent<Unity.Transforms.LocalTransform>(entity).Position;


        }

        foreach (var kvp in refCountDeltas)
        {
            Entity field = kvp.Key;
            int delta = kvp.Value;
            if (delta == 0) continue;

            if (newFields.Contains(field))
            {
                ecb.SetComponent(field, new FlowFieldRefCount { value = delta });
            }
            else if (state.EntityManager.Exists(field) && state.EntityManager.HasComponent<FlowFieldRefCount>(field))
            {
                var refCount = state.EntityManager.GetComponentData<FlowFieldRefCount>(field);
                refCount.value += delta;
                ecb.SetComponent(field, refCount);
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
        refCountDeltas.Dispose();
        newFields.Dispose();
    }
}
