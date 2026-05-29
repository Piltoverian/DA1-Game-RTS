using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct BuildingCostArea : IComponentData
{
    public float3 CenterOffset;
    public float3 HalfExtents;
}

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
public partial struct HealthDeadTestSystem : ISystem
{
    private const int BuildingClearCost = 1;
    private const float BuildingCostPadding = 0.01f;

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

        ComponentLookup<BuildingCostArea> buildingCostAreaLookup =
            SystemAPI.GetComponentLookup<BuildingCostArea>(true);

        ComponentLookup<LocalTransform> localTransformLookup =
            SystemAPI.GetComponentLookup<LocalTransform>(true);

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

            TrySendBuildingClearCostRequest(
                state.EntityManager,
                entity,
                rootEntity,
                buildingLookup,
                buildingCostAreaLookup,
                localTransformLookup,
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

        int newPopulation = math.max(0, playerContext.currentPopulation - 1);

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

        // Building có thể cũng dùng Unit/playerID để biết chủ sở hữu.
        // Không trừ population khi building chết.
        if (buildingLookup.HasComponent(entity))
            return false;

        return true;
    }

    private static void TrySendBuildingClearCostRequest(
        EntityManager entityManager,
        Entity deadEntity,
        Entity rootEntity,
        ComponentLookup<BuildingData> buildingLookup,
        ComponentLookup<BuildingCostArea> buildingCostAreaLookup,
        ComponentLookup<LocalTransform> localTransformLookup,
        BufferLookup<LinkedEntityGroup> linkedGroupLookup)
    {
        if (!TryGetBuildingCostAreaForClear(
                deadEntity,
                rootEntity,
                buildingLookup,
                buildingCostAreaLookup,
                localTransformLookup,
                linkedGroupLookup,
                out float3 footprintCenter,
                out float3 halfExtents))
        {
            return;
        }

        SendGridCostChangeRequest(
            entityManager,
            footprintCenter,
            halfExtents,
            BuildingClearCost
        );
    }

    private static bool TryGetBuildingCostAreaForClear(
        Entity deadEntity,
        Entity rootEntity,
        ComponentLookup<BuildingData> buildingLookup,
        ComponentLookup<BuildingCostArea> buildingCostAreaLookup,
        ComponentLookup<LocalTransform> localTransformLookup,
        BufferLookup<LinkedEntityGroup> linkedGroupLookup,
        out float3 footprintCenter,
        out float3 halfExtents)
    {
        if (TryReadBuildingCostAreaAndPosition(
                rootEntity,
                buildingLookup,
                buildingCostAreaLookup,
                localTransformLookup,
                out footprintCenter,
                out halfExtents))
        {
            return true;
        }

        if (TryReadBuildingCostAreaAndPosition(
                deadEntity,
                buildingLookup,
                buildingCostAreaLookup,
                localTransformLookup,
                out footprintCenter,
                out halfExtents))
        {
            return true;
        }

        if (linkedGroupLookup.HasBuffer(rootEntity))
        {
            DynamicBuffer<LinkedEntityGroup> linkedEntities =
                linkedGroupLookup[rootEntity];

            for (int i = 0; i < linkedEntities.Length; i++)
            {
                Entity linkedEntity = linkedEntities[i].Value;

                if (TryReadBuildingCostAreaAndPosition(
                        linkedEntity,
                        buildingLookup,
                        buildingCostAreaLookup,
                        localTransformLookup,
                        out footprintCenter,
                        out halfExtents))
                {
                    return true;
                }
            }
        }

        footprintCenter = default;
        halfExtents = default;
        return false;
    }

    private static bool TryReadBuildingCostAreaAndPosition(
        Entity entity,
        ComponentLookup<BuildingData> buildingLookup,
        ComponentLookup<BuildingCostArea> buildingCostAreaLookup,
        ComponentLookup<LocalTransform> localTransformLookup,
        out float3 footprintCenter,
        out float3 halfExtents)
    {
        footprintCenter = default;
        halfExtents = default;

        if (entity == Entity.Null)
            return false;

        if (!buildingLookup.HasComponent(entity))
            return false;

        if (!localTransformLookup.HasComponent(entity))
            return false;

        LocalTransform transform = localTransformLookup[entity];
        BuildingData buildingData = buildingLookup[entity];

        if (buildingCostAreaLookup.HasComponent(entity))
        {
            BuildingCostArea costArea = buildingCostAreaLookup[entity];

            footprintCenter = transform.Position + costArea.CenterOffset;
            halfExtents = costArea.HalfExtents;
            return true;
        }

        // Fallback cho building cũ chưa có BuildingCostArea.
        halfExtents = new float3(
            buildingData.FootprintSizeX * 0.5f,
            buildingData.BlockerHeight * 0.5f,
            buildingData.FootprintSizeZ * 0.5f
        );

        footprintCenter = transform.Position + new float3(0f, halfExtents.y, 0f);
        return true;
    }

    private static void SendGridCostChangeRequest(
        EntityManager entityManager,
        float3 footprintCenter,
        float3 halfExtents,
        int newCost)
    {
        EntityQuery gridQuery = entityManager.CreateEntityQuery(
            typeof(GridComponent),
            typeof(CostChangeRequest)
        );

        if (gridQuery.IsEmpty)
            return;

        Entity gridEntity = gridQuery.GetSingletonEntity();

        if (!entityManager.HasBuffer<CostChangeRequest>(gridEntity))
            return;

        GridComponent grid = entityManager.GetComponentData<GridComponent>(gridEntity);

        float padding = BuildingCostPadding;

        if (grid.cellsize > 0f)
            padding = math.min(padding, grid.cellsize * 0.45f);

        float minX = footprintCenter.x - halfExtents.x + padding;
        float minZ = footprintCenter.z - halfExtents.z + padding;
        float maxX = footprintCenter.x + halfExtents.x - padding;
        float maxZ = footprintCenter.z + halfExtents.z - padding;

        if (minX > maxX)
        {
            minX = footprintCenter.x - halfExtents.x;
            maxX = footprintCenter.x + halfExtents.x;
        }

        if (minZ > maxZ)
        {
            minZ = footprintCenter.z - halfExtents.z;
            maxZ = footprintCenter.z + halfExtents.z;
        }

        StartEndRect area = new StartEndRect(new float2(minX, minZ));
        area.ExpandTo(new float2(maxX, maxZ));

        DynamicBuffer<CostChangeRequest> requestBuffer =
            entityManager.GetBuffer<CostChangeRequest>(gridEntity);

        requestBuffer.Add(new CostChangeRequest
        {
            newCost = newCost,
            area = area
        });
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
