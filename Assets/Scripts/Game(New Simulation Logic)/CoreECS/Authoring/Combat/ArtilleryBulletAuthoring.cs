using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class ArtilleryBulletAuthoring : MonoBehaviour
{
    public float speed = 15f;
    public float aoeDamage = 50f;
    public float aoeRadius = 3f;
    public float maxHeight = 5f;

    public class Baker : Baker<ArtilleryBulletAuthoring>
    {
        public override void Bake(ArtilleryBulletAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new ArtilleryBullet
            {
                speed = authoring.speed,
                aoeDamage = authoring.aoeDamage,
                aoeRadius = authoring.aoeRadius,
                maxHeight = authoring.maxHeight,
                distance = 0f 
            });
        }
    }
}
public struct ArtilleryBullet : IComponentData
{
    public float speed;
    public float aoeDamage;
    public float aoeRadius;
    public float maxHeight; 

    public float3 startPosition;
    public float3 targetPosition;
    public float distance;
    public float distanceTraveled;
}