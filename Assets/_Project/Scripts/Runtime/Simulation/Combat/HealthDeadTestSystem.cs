using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
public partial struct HealthDeadTestSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        BufferLookup<LinkedEntityGroup> linkedGroupLookup =
            SystemAPI.GetBufferLookup<LinkedEntityGroup>(true);

        ComponentLookup<Parent> parentLookup =
            SystemAPI.GetComponentLookup<Parent>(true);

        ComponentLookup<EntityOwner> unitLookup =
            SystemAPI.GetComponentLookup<EntityOwner>(true);

        ComponentLookup<BuildingComponent> buildingLookup =
            SystemAPI.GetComponentLookup<BuildingComponent>(true);

        NativeHashSet<Entity> destroyedRoots =
            new NativeHashSet<Entity>(32, Allocator.Temp);

        foreach (var (health, entity) in
                 SystemAPI.Query<RefRO<EntityHealth>>()
                     .WithEntityAccess())
        {
            if (health.ValueRO.CurrentHP > 0f)
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

