using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateBefore(typeof(ShootAttackSystem))]
[BurstCompile]
public partial struct MoveOverrideSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridComponent>();
        state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        GridComponent grid = SystemAPI.GetSingleton<GridComponent>();

        EntityCommandBuffer ecb = SystemAPI
            .GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        EntityManager entityManager = state.EntityManager;

        foreach (var (
                     localTransform,
                     moveOverride,
                     moveOverrideEnabled,
                     moveAgent,
                     steering,
                     entity)
                 in SystemAPI.Query<
                         RefRO<LocalTransform>,
                         RefRW<MoveOverride>,
                         EnabledRefRW<MoveOverride>,
                         RefRO<MovementAgentComponent>,
                         RefRO<MovementSteeringComponent>>()
                     .WithEntityAccess())
        {
            float3 currentPos = localTransform.ValueRO.Position;
            float3 targetPos = moveOverride.ValueRO.targetPosition;

            currentPos.y = 0f;
            targetPos.y = 0f;

            float distanceSq = math.distancesq(currentPos, targetPos);

            float stopDistance = math.max(0.01f, steering.ValueRO.stoppingDistance);
            float stopDistanceSq = stopDistance * stopDistance;

            bool reachedByDistance =
                distanceSq <= stopDistanceSq;

            bool movementAlreadyStopped =
                moveOverride.ValueRO.targetApplied &&
                !moveAgent.ValueRO.hastarget;

            bool movementSettled =
                moveOverride.ValueRO.targetApplied &&
                steering.ValueRO.isSettled;

            if (reachedByDistance || movementAlreadyStopped || movementSettled)
            {
                moveOverride.ValueRW.targetApplied = false;
                moveOverrideEnabled.ValueRW = false;

                MovementAgentAPI.StopAgent(
                    entityManager,
                    entity,
                    ecb
                );

                continue;
            }

            if (!moveOverride.ValueRO.targetApplied)
            {
                TargetChangeResult result = MovementAgentAPI.SetTarget(
                    entityManager,
                    entity,
                    moveOverride.ValueRO.targetPosition,
                    grid,
                    ecb
                );

                if (result == TargetChangeResult.Success)
                {
                    moveOverride.ValueRW.targetApplied = true;
                }
                else
                {
                    moveOverride.ValueRW.targetApplied = false;
                    moveOverrideEnabled.ValueRW = false;

                    MovementAgentAPI.StopAgent(
                        entityManager,
                        entity,
                        ecb
                    );
                }
            }
        }
    }
}