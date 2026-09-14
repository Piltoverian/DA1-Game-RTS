using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class TargetCacheAuthoring : MonoBehaviour
{
    public class Baker : Baker<TargetCacheAuthoring>
    {
        public override void Bake(TargetCacheAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new TargetCache
            {
                targetEntity = Entity.Null,
                lastTargetPosition = float3.zero
            });
        }
    }
}

public struct TargetCache : IComponentData
{
    public Entity targetEntity;
    public float3 lastTargetPosition;
}
