using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;

public static class TrainUnitHelper
{
    public static void TrainUnit(EntityManager entityManager, Entity buildingEntity, int indexInPrefabList)
    {
        if (BuildingHelper.IsUnderConstruction(entityManager, buildingEntity))
        {
            Debug.Log("Building is still under construction.");
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
            Debug.Log("Selected building is not a production building.");
            return;
        }
        var queueBuffer = entityManager.GetBuffer<ProductionQueueElement>(buildingEntity);
        var prefabBuffer = entityManager.GetBuffer<ProductionElement>(buildingEntity);
        if (queueBuffer.Length >= prod.MaxQueue)
        {
            Debug.Log("Production queue full.");
            return;
        }
        if (indexInPrefabList < 0 || indexInPrefabList >= prefabBuffer.Length)
        {
            Debug.Log("Invalid unit prefab index.");
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
            Debug.Log("Not enough resources to train.");
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
        Debug.Log("Queued unit. Queue = " + queueBuffer.Length);
    }
}

public static class BuildingHelper
{
    public static bool IsUnderConstruction(EntityManager entityManager, Entity buildingEntity)
    {
        if (!entityManager.Exists(buildingEntity))
            return false;
        return entityManager.HasComponent<UnderConstructionTag>(buildingEntity);
    }
}