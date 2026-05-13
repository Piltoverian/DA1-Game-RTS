using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateBefore(typeof(ShootAttackSystem))]
[BurstCompile]
public partial struct MoveOverrideSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var grid = SystemAPI.GetSingleton<GridComponent>();

        var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

        var entityManager = state.EntityManager;

        foreach (var (localTransform, moveOverride, moveOverrideEnabled, entity)
            in SystemAPI.Query<
                RefRO<LocalTransform>,
                RefRW<MoveOverride>,
                EnabledRefRW<MoveOverride>
            >()
            .WithEntityAccess())
        {
            float distanceSq = math.distancesq(
                localTransform.ValueRO.Position,
                moveOverride.ValueRO.targetPosition);

            if (distanceSq >  moveOverride.ValueRO.stopDistanceSq)
            {
 
                if (!moveOverride.ValueRO.targetApplied)
                {
                    MovementAgentAPI.SetTarget(
                        entityManager,
                        entity,
                        moveOverride.ValueRO.targetPosition,
                        grid,
                        ecb);

                    moveOverride.ValueRW.targetApplied = true;
                }
            }
            else
            {
                moveOverrideEnabled.ValueRW = false;

                MovementAgentAPI.StopAgent(
                    entityManager,
                    entity,
                    ecb);
            }
        }
    }
}