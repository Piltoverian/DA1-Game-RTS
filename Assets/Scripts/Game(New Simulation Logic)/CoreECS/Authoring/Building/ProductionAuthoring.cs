using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class ProductionAuthoring : MonoBehaviour
{
    public float ProductionTime = 5f;
    public int MaxQueue = 5;

    [Header("Spawn / Rally Markers")]
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

            float3 spawnLocalOffset = float3.zero;
            float3 rallyLocalOffset = new float3(3f, 0f, 3f);

            if (src.SpawnOffset != null)
            {
                spawnLocalOffset = src.transform.InverseTransformPoint(
                    src.SpawnOffset.position
                );
            }

            if (src.RallyOffset != null)
            {
                rallyLocalOffset = src.transform.InverseTransformPoint(
                    src.RallyOffset.position
                );
            }

            AddComponent(e, new ProductionData
            {
                ProductionTime = src.ProductionTime,
                TimeRemaining = 0f,
                MaxQueue = src.MaxQueue,

                // Lưu LOCAL offset, không lưu world position
                SpawnOffset = spawnLocalOffset,
                RallyOffset = rallyLocalOffset,

                UnitGoldCost = src.UnitGoldCost,
                UnitFoodCost = src.UnitFoodCost
            });

            DynamicBuffer<ProductionElement> productionBuffer =
                AddBuffer<ProductionElement>(e);

            if (src.UnitPrefabs != null)
            {
                foreach (GameObject prefab in src.UnitPrefabs)
                {
                    if (prefab == null)
                        continue;

                    productionBuffer.Add(new ProductionElement
                    {
                        UnitPrefab = GetEntity(
                            prefab,
                            TransformUsageFlags.Dynamic
                        )
                    });
                }
            }

            AddBuffer<ProductionQueueElement>(e);
        }
    }
}

public struct ProductionData : IComponentData
{
    public float ProductionTime;
    public float TimeRemaining;
    public int MaxQueue;

    // Đây là LOCAL offset so với building
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