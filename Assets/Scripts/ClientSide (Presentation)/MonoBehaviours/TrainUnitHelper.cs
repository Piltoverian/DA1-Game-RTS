using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;

public static class TrainUnitHelper
{
    public static void TrainUnit(EntityManager entityManager, Entity buildingEntity, int indexInPrefabList)
    {
        if (BuildingHelper.IsUnderConstruction(entityManager, buildingEntity))
        {
            return;
        }
        if (!entityManager.Exists(buildingEntity))
            return;
        ProductionData prod;
        if (entityManager.HasComponent<ProductionData>(buildingEntity))
        {
            prod =
                entityManager.GetComponentData<ProductionData>(buildingEntity);
        }
        else
        {
            return;
        }
        var queueBuffer = entityManager.GetBuffer<ProductionQueueElement>(buildingEntity);
        var prefabBuffer = entityManager.GetBuffer<ProductionElement>(buildingEntity);
        if (queueBuffer.Length >= prod.MaxQueue)
        {
            return;
        }
        if (indexInPrefabList < 0 || indexInPrefabList >= prefabBuffer.Length)
        {
            return;
        }

        int playerId = 0;
        if (entityManager.HasComponent<Unit>(buildingEntity))
        {
            playerId = entityManager.GetComponentData<Unit>(buildingEntity).playerID;
        }

        float gold = 0;
        float food = 0;
        PlayerContextHelper.GetPlayerResourceByType(entityManager, playerId, ResourceType.Gold, out gold);
        PlayerContextHelper.GetPlayerResourceByType(entityManager, playerId, ResourceType.Food, out food);

        if (gold < prod.UnitGoldCost || food < prod.UnitFoodCost)
        {
            return;
        }

        PlayerContextHelper.SetPlayerResource(entityManager, playerId, ResourceType.Gold, gold - prod.UnitGoldCost);
        PlayerContextHelper.SetPlayerResource(entityManager, playerId, ResourceType.Food, food - prod.UnitFoodCost);

        queueBuffer.Add(new ProductionQueueElement
        {
            UnitPrefab = prefabBuffer[indexInPrefabList].UnitPrefab
        });

        if (prod.TimeRemaining <= 0f)
        {
            prod.TimeRemaining = prod.ProductionTime;
        }
        entityManager.SetComponentData(buildingEntity, prod);
    }
}

public static class BuildingHelper
{
    public static bool IsUnderConstruction(EntityManager entityManager, Entity buildingEntity)
    {
        if (!entityManager.Exists(buildingEntity))
            return false;
        if (!entityManager.HasComponent<BuildingStateComponent>(buildingEntity))
            return false;
        var state = entityManager.GetComponentData<BuildingStateComponent>(buildingEntity);
        return state.Current != BuildingState.Completed;
    }

    public static bool CanBuildOrRepair(EntityManager entityManager, Entity buildingEntity)
    {
        if (!entityManager.Exists(buildingEntity))
            return false;
        if (!entityManager.HasComponent<BuildingStateComponent>(buildingEntity))
            return false;
        var state = entityManager.GetComponentData<BuildingStateComponent>(buildingEntity);
        if (state.Current == BuildingState.StartBuild || state.Current == BuildingState.UnderConstruction)
            return true;
        if (state.Current == BuildingState.Completed && entityManager.HasComponent<Health>(buildingEntity))
        {
            var h = entityManager.GetComponentData<Health>(buildingEntity);
            return h.healthAmount < h.maxHealthAmount;
        }
        return false;
    }
}