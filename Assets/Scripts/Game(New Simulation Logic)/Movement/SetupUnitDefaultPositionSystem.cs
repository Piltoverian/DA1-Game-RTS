using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

[UpdateInGroup(typeof(InitializationSystemGroup))]
[BurstCompile]
partial struct SetupUnitMoverDefaultPositionSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var gridComponent = SystemAPI.GetSingleton<GridComponent>();

        var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

        var em = state.EntityManager;

        foreach (var (transform, agent, entity)
                 in SystemAPI.Query<RefRO<LocalTransform>, RefRW<MovementAgentComponent>>()
                     .WithAll<SetupUnitMoverDefaultPosition>()
                     .WithEntityAccess())
        {
            MovementAgentAPI.SetTarget(em, entity, transform.ValueRO.Position, gridComponent, ecb);
            

            ecb.RemoveComponent<SetupUnitMoverDefaultPosition>(entity);
        }
    }
}
