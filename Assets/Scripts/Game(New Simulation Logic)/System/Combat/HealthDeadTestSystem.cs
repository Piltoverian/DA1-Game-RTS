using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(LateSimulationSystemGroup))]
public partial struct HealthDeadTestSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        BufferLookup<LinkedEntityGroup> linkedGroupLookup =
            SystemAPI.GetBufferLookup<LinkedEntityGroup>(true);

        ComponentLookup<Parent> parentLookup =
            SystemAPI.GetComponentLookup<Parent>(true);

        NativeHashSet<Entity> destroyedRoots =
            new NativeHashSet<Entity>(32, Allocator.Temp);

        foreach (var (health, entity) in
                 SystemAPI.Query<RefRO<Health>>()
                     .WithEntityAccess())
        {
            if (health.ValueRO.healthAmount > 0f)
                continue;

            Entity rootEntity = GetDestroyRoot(
                entity, 
                parentLookup,
                linkedGroupLookup
            );

            if (destroyedRoots.Contains(rootEntity))
                continue;

            destroyedRoots.Add(rootEntity);

            DestroyEntityWithLinkedGroup(
                rootEntity,
                linkedGroupLookup,
                ecb
            );
        }

        destroyedRoots.Dispose();
    }

    private static Entity GetDestroyRoot(
        Entity entity,
        ComponentLookup<Parent> parentLookup,
        BufferLookup<LinkedEntityGroup> linkedGroupLookup)
    {
        Entity current = entity;
        Entity bestRoot = entity;

        for (int i = 0; i < 16; i++)
        {
            if (linkedGroupLookup.HasBuffer(current))
            {
                bestRoot = current;
            }

            if (!parentLookup.HasComponent(current))
            {
                bestRoot = current;
                break;
            }

            current = parentLookup[current].Value;
        }

        return bestRoot;
    }

    private static void DestroyEntityWithLinkedGroup(
        Entity rootEntity,
        BufferLookup<LinkedEntityGroup> linkedGroupLookup,
        EntityCommandBuffer ecb)
    {
        if (linkedGroupLookup.HasBuffer(rootEntity))
        {
            DynamicBuffer<LinkedEntityGroup> linkedEntities =
                linkedGroupLookup[rootEntity];

            for (int i = 0; i < linkedEntities.Length; i++)
            {
                Entity linkedEntity = linkedEntities[i].Value;

                if (linkedEntity == Entity.Null)
                    continue;

                ecb.DestroyEntity(linkedEntity);
            }

            return;
        }

        ecb.DestroyEntity(rootEntity);
    }
}