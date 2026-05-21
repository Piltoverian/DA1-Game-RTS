using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Transforms;
using NUnit.Framework;
using System.Collections.Generic;
public class ProductionAuthoring : MonoBehaviour
{

    public float ProductionTime = 5f;
    public int MaxQueue = 5;

    public Transform SpawnOffset;
    public Transform RallyOffset;

    [Header("Unit Cost")]
    public int UnitGoldCost = 50;
    public int UnitFoodCost = 0;

    [Header("Production")]
    public List<GameObject> UnitPrefabs;
    class Baker : Baker<ProductionAuthoring>
    {
        public override void Bake(ProductionAuthoring src)
        {
            Entity e = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(e, new ProductionData
            {
                ProductionTime = src.ProductionTime,
                TimeRemaining = 0f,
                MaxQueue = src.MaxQueue,
                SpawnOffset = src.SpawnOffset.position,
                RallyOffset = src.RallyOffset.position,
                UnitGoldCost = src.UnitGoldCost,
                UnitFoodCost = src.UnitFoodCost
            });

            DynamicBuffer<ProductionElement> productionBuffer = AddBuffer<ProductionElement>(e);
            foreach (GameObject prefab in src.UnitPrefabs)
            {
                productionBuffer.Add(new ProductionElement { UnitPrefab = GetEntity(prefab, TransformUsageFlags.Dynamic) });
            }

            DynamicBuffer<ProductionQueueElement> productionQueueBuffer = AddBuffer<ProductionQueueElement>(e);
        }
    }
}

public struct ProductionData : IComponentData
{
    public float ProductionTime;
    public float TimeRemaining;
    public int MaxQueue;

    public float3 SpawnOffset;
    public float3 RallyOffset;

    public int UnitGoldCost;
    public int UnitFoodCost;
}

public struct UnitTag : IComponentData
{
}

public struct ProductionElement : IBufferElementData
{
    public Entity UnitPrefab;
}

public struct ProductionQueueElement : IBufferElementData
{
    public Entity UnitPrefab;
}