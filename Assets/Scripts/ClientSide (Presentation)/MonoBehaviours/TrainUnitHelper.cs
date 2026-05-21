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
        queueBuffer.Add(new ProductionQueueElement
        {
            UnitPrefab = prefabBuffer[indexInPrefabList].UnitPrefab
        });
        
        EntityQuery query =
            entityManager.CreateEntityQuery(typeof(PlayerResourceData));
        if (query.IsEmpty)
        {
            Debug.LogWarning("PlayerResourceData not found.");
            return;
        }
        Entity resEntity = query.GetSingletonEntity();
        PlayerResourceData res =
            entityManager.GetComponentData<PlayerResourceData>(resEntity);
        if (res.Gold < prod.UnitGoldCost ||
            res.Food < prod.UnitFoodCost)
        {
            Debug.Log("Not enough resources to train.");
            return;
        }
        res.Gold -= prod.UnitGoldCost;
        res.Food -= prod.UnitFoodCost;
        entityManager.SetComponentData(resEntity, res);
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