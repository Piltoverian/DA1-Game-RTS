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
    private EntityQuery m_RequestQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        m_RequestQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<TargetChangeRequest, MovementAgentComponent, MovementSteeringComponent>()
            .Build(ref state);

        state.RequireForUpdate<GridComponent>();
        state.RequireForUpdate<FlowFieldCache>();
        state.RequireForUpdate(m_RequestQuery);
    }

    public void OnUpdate(ref SystemState state)
    {
        if (m_RequestQuery.IsEmptyIgnoreFilter)
            return;

        var grid = SystemAPI.GetSingleton<GridComponent>();
        var gridEntity = SystemAPI.GetSingletonEntity<GridComponent>();
        var gridCosts = SystemAPI.GetBuffer<GridNodeCost>(gridEntity).AsNativeArray();
        var cacheEntity = SystemAPI.GetSingletonEntity<FlowFieldCache>();
        var cacheBuffer = SystemAPI.GetBuffer<FlowFieldCacheEntry>(cacheEntity);
        var fixedFrameCount = SystemAPI.GetSingleton<FixedFrameCount>();

        var blockageQuery = SystemAPI.QueryBuilder().WithAll<BlockageData, LocalToWorld>().Build();
        var blockageDatas = blockageQuery.ToComponentDataArray<BlockageData>(Allocator.Temp);
        var blockageLocalToWorlds = blockageQuery.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
        
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var refCountDeltas = new NativeHashMap<Entity, int>(16, Allocator.Temp);
        var newFields = new NativeHashSet<Entity>(16, Allocator.Temp);

        foreach (var (request, move, steering, entity) in 
                 SystemAPI.Query<RefRO<TargetChangeRequest>, RefRW<MovementAgentComponent>, RefRW<MovementSteeringComponent>>().WithEntityAccess())
        {
            float3 rawWorldTarget = request.ValueRO.newWorldTarget;

            if (!NaturalBlockedTargetResolver.TryResolveTarget(
                    rawWorldTarget,
                    grid,
                    gridCosts,
                    blockageDatas,
                    blockageLocalToWorlds,
                    out int2 targetCell,
                    out float3 resolvedWorldTarget,
                    out TargetResolutionKind resolutionKind))
            {
                FlowFieldHelper.AssignFieldToMoveComponent(
                    ref move.ValueRW,
                    ref steering.ValueRW,
                    Entity.Null,
                    rawWorldTarget,
                    entity,
                    ecb,
                    state.EntityManager,
                    ref refCountDeltas);

                move.ValueRW.navigationTargetCell = targetCell;
                move.ValueRW.targetResolutionGeneration = grid.generation;
                move.ValueRW.targetResolutionKind = TargetResolutionKind.None;
                move.ValueRW.realTarget = rawWorldTarget;
                move.ValueRW.velocity = float3.zero;
                move.ValueRW.preferredVelocity = float3.zero;
                steering.ValueRW.isSettled = true;
                steering.ValueRW.stuckTime = 0;
                steering.ValueRW.minDistanceToTarget = float.MaxValue;
                steering.ValueRW.lastPosition = SystemAPI.GetComponent<Unity.Transforms.LocalTransform>(entity).Position;
                continue;
            }
            
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
                    resolvedWorldTarget, 
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
                rawWorldTarget, // TRUYỀN TỌA ĐỘ ĐÍCH VÀO ĐÂY
                entity,
                ecb, 
                state.EntityManager,
                ref refCountDeltas);

            move.ValueRW.navigationTargetCell = targetCell;
            move.ValueRW.targetResolutionGeneration = grid.generation;
            move.ValueRW.targetResolutionKind = resolutionKind;
            move.ValueRW.realTarget = resolvedWorldTarget;

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
                ecb.SetComponent(field, new FlowFieldRefCount { value = math.max(0, delta) });
            }
            else if (state.EntityManager.Exists(field) && state.EntityManager.HasComponent<FlowFieldRefCount>(field))
            {
                var refCount = state.EntityManager.GetComponentData<FlowFieldRefCount>(field);
                refCount.value = math.max(0, refCount.value + delta);
                ecb.SetComponent(field, refCount);
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
        refCountDeltas.Dispose();
        newFields.Dispose();
        blockageDatas.Dispose();
        blockageLocalToWorlds.Dispose();
    }
}
