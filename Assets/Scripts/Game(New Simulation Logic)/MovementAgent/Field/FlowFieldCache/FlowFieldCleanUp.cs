using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
[UpdateAfter(typeof(MovementAgentActuatorSystem))]
public partial struct FlowFieldCleanupSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // cache singleton
        Entity cacheEntity = SystemAPI.GetSingletonEntity<FlowFieldCache>();
        DynamicBuffer<FlowFieldCacheEntry> cache = em.GetBuffer<FlowFieldCacheEntry>(cacheEntity);

        foreach (var (refCount, entity)
            in SystemAPI.Query<RefRO<FlowFieldRefCount>>()
            .WithEntityAccess())
        {
            if (!em.Exists(entity))
                continue;

            if (refCount.ValueRO.value > 0)
                continue;

            bool inCache = false;
            for (int i = 0; i < cache.Length; i++)
            {
                if (cache[i].flowField == entity)
                {
                    inCache = true;
                    break;
                }
            }

            if (inCache)
                continue;

            ecb.DestroyEntity(entity);
        }

        ecb.Playback(em);
        ecb.Dispose();
    }
}