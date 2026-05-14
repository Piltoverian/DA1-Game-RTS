using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Transforms; 
public class ProductionAuthoring : MonoBehaviour
{
    public GameObject UnitPrefab;

    public float ProductionTime = 5f;
    public int MaxQueue = 5;

    public Transform SpawnOffset;
    public Transform RallyOffset;

    [Header("Unit Cost")]
    public int UnitGoldCost = 50;
    public int UnitFoodCost = 0;

    class Baker : Baker<ProductionAuthoring>
    {
        public override void Bake(ProductionAuthoring src)
        {
            Entity e = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(e, new ProductionData
            {
                UnitPrefab = GetEntity(src.UnitPrefab, TransformUsageFlags.Dynamic),
                ProductionTime = src.ProductionTime,
                TimeRemaining = 0f,
                QueueCount = 0,
                MaxQueue = src.MaxQueue,
                SpawnOffset = src.SpawnOffset.position,
                RallyOffset = src.RallyOffset.position,
                UnitGoldCost = src.UnitGoldCost,
                                UnitFoodCost = src.UnitFoodCost
            });
        }
    }
}

public struct ProductionData : IComponentData
{
    public Entity UnitPrefab;

    public float ProductionTime;
    public float TimeRemaining;

    public int QueueCount;
    public int MaxQueue;

    public float3 SpawnOffset;
    public float3 RallyOffset;

    public int UnitGoldCost;
    public int UnitFoodCost;
}

public struct UnitTag : IComponentData
{
}