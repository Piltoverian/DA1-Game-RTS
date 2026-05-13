using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
public class ProductionAuthoring : MonoBehaviour
{
    public GameObject UnitPrefab;

    public float ProductionTime = 5f;

    public Vector3 SpawnOffset = new Vector3(0, 0, 8);

    class Baker : Baker<ProductionAuthoring>
    {
        public override void Bake(ProductionAuthoring src)
        {
            Entity e = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(e, new ProductionData
            {
                UnitPrefab =
                    GetEntity(src.UnitPrefab, TransformUsageFlags.Dynamic),

                ProductionTime = src.ProductionTime,

                TimeRemaining = 0f,

                QueueCount = 0,

                SpawnOffset = src.SpawnOffset
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

    public float3 SpawnOffset;
}