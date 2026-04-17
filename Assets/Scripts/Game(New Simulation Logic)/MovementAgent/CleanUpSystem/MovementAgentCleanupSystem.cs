using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
public partial struct MovementAgentCleanupSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (cleanup, entity) in SystemAPI.Query<RefRO<MovementAgentFieldCleanUpData>>()
            .WithNone<MovementAgentComponent>()
            .WithEntityAccess())
        {
            if (cleanup.ValueRO.FieldEntity != Entity.Null && state.EntityManager.Exists(cleanup.ValueRO.FieldEntity))
            {
                if (state.EntityManager.HasComponent<FlowFieldRefCount>(cleanup.ValueRO.FieldEntity))
                {
                    var refCount = state.EntityManager.GetComponentData<FlowFieldRefCount>(cleanup.ValueRO.FieldEntity);
                    refCount.value--;
                    ecb.SetComponent(cleanup.ValueRO.FieldEntity, refCount);
                }
            }

            ecb.RemoveComponent<MovementAgentFieldCleanUpData>(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
