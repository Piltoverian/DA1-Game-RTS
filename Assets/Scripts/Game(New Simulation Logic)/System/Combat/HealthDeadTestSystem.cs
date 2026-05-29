using Unity.Collections;
using Unity.Entities;
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

        ComponentLookup<Unit> unitLookup =
            SystemAPI.GetComponentLookup<Unit>(true);

        ComponentLookup<BuildingData> buildingLookup =
            SystemAPI.GetComponentLookup<BuildingData>(true);

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

            TryDecreasePopulationOnDeath(
                state.EntityManager,
                entity,
                rootEntity,
                unitLookup,
                buildingLookup,
                linkedGroupLookup
            );

            DestroyEntityWithLinkedGroup(
                rootEntity,
                linkedGroupLookup,
                ecb
            );
        }

        destroyedRoots.Dispose();
    }

    private static void TryDecreasePopulationOnDeath(
        EntityManager entityManager,
        Entity deadEntity,
        Entity rootEntity,
        ComponentLookup<Unit> unitLookup,
        ComponentLookup<BuildingData> buildingLookup,
        BufferLookup<LinkedEntityGroup> linkedGroupLookup)
    {
        if (!TryGetPopulationUnit(
                deadEntity,
                rootEntity,
                unitLookup,
                buildingLookup,
                linkedGroupLookup,
                out Unit unit))
        {
            return;
        }

        PlayerContext playerContext;

        FunctionResult result = PlayerContextHelper.GetContextData(
            entityManager,
            unit.playerID,
            out playerContext
        );

        if (result == FunctionResult.Failure)
            return;

        int newPopulation = playerContext.currentPopulation - 1;

        if (newPopulation < 0)
            newPopulation = 0;

        PlayerContextHelper.UpdatePlayerContext(
            entityManager,
            playerContext.PlayerId,
            PlayerContextDataType.currentPopulation,
            newPopulation
        );
    }

    private static bool TryGetPopulationUnit(
        Entity deadEntity,
        Entity rootEntity,
        ComponentLookup<Unit> unitLookup,
        ComponentLookup<BuildingData> buildingLookup,
        BufferLookup<LinkedEntityGroup> linkedGroupLookup,
        out Unit unit)
    {
        unit = default;

        if (IsValidPopulationUnit(rootEntity, unitLookup, buildingLookup))
        {
            unit = unitLookup[rootEntity];
            return true;
        }

        if (IsValidPopulationUnit(deadEntity, unitLookup, buildingLookup))
        {
            unit = unitLookup[deadEntity];
            return true;
        }

        if (linkedGroupLookup.HasBuffer(rootEntity))
        {
            DynamicBuffer<LinkedEntityGroup> linkedEntities =
                linkedGroupLookup[rootEntity];

            for (int i = 0; i < linkedEntities.Length; i++)
            {
                Entity linkedEntity = linkedEntities[i].Value;

                if (IsValidPopulationUnit(linkedEntity, unitLookup, buildingLookup))
                {
                    unit = unitLookup[linkedEntity];
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsValidPopulationUnit(
        Entity entity,
        ComponentLookup<Unit> unitLookup,
        ComponentLookup<BuildingData> buildingLookup)
    {
        if (entity == Entity.Null)
            return false;

        if (!unitLookup.HasComponent(entity))
            return false;

        // Building cũng có thể đang dùng Unit để lưu playerID.
        // Vì vậy phải chặn BuildingData để nhà chết không bị trừ dân.
        if (buildingLookup.HasComponent(entity))
            return false;

        return true;
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