using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

[UpdateAfter(typeof(SelectSystem))]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
partial struct SelecUISystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        
    }

    public void OnUpdate(ref SystemState state)
    {

        foreach (var (selected, entity) in
                 SystemAPI.Query<RefRW<Selected>>().WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).WithEntityAccess())
        {
            var visualEntity = selected.ValueRO.visualEntity;
            if (visualEntity == Entity.Null || !state.EntityManager.Exists(visualEntity))
            {
                continue;
            }

            if (!state.EntityManager.HasComponent<SpriteRenderer>(visualEntity))
            {
                continue;
            }

            var renderer = state.EntityManager.GetComponentObject<SpriteRenderer>(visualEntity);
            
            if (!state.EntityManager.IsComponentEnabled<Selected>(entity))
            {
                renderer.enabled = false;
                continue;
            }
            renderer.enabled = true;
        }
    }
    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
