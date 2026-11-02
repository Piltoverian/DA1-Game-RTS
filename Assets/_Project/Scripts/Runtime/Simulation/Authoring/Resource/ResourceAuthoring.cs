using Unity.Entities;
using UnityEngine;
using Unity.NetCode;

public class ResourceAuthoring : MonoBehaviour
{
    public ResourceType ResourceType = ResourceType.Gold;

    class Baker : Baker<ResourceAuthoring>
    {
        public override void Bake(ResourceAuthoring src)
        {
            Entity e = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(e, new ResourceNodeData
            {
                Type = src.ResourceType,
                Amount = 0 // Template stock is assigned by the spawning configuration.
            });

            AddComponent<ResourceNodeTag>(e);
            AddComponent<ResourceNodePendingConfig>(e);
        }
    }
}

[GhostComponent(OwnerSendType = SendToOwnerType.All)]
public struct ResourceNodeData : IComponentData
{
    public ResourceType Type;
    [GhostField] public int Amount;
}

public struct ResourceNodeTag : IComponentData
{
}

// An unconfigured template/placed node must not be treated as a depleted live mine.
public struct ResourceNodePendingConfig : IComponentData { }
