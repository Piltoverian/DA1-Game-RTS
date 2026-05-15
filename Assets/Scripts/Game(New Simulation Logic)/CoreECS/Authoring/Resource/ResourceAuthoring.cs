using Unity.Entities;
using UnityEngine;

public class ResourceAuthoring : MonoBehaviour
{
    public ResourceType ResourceType = ResourceType.Gold;
    public int Amount = 1000;

    class Baker : Baker<ResourceAuthoring>
    {
        public override void Bake(ResourceAuthoring src)
        {
            Entity e = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(e, new ResourceNodeData
            {
                Type = src.ResourceType,
                Amount = src.Amount
            });

            AddComponent<ResourceNodeTag>(e);
        }
    }
}

public struct ResourceNodeData : IComponentData
{
    public ResourceType Type;
    public int Amount;
}

public struct ResourceNodeTag : IComponentData
{
}
