using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;


[GhostComponent(OwnerSendType =SendToOwnerType.All)]
public struct EntityOwner : IComponentData
{
    [GhostField]
    public int PlayerID;
}

[GhostComponent(OwnerSendType = SendToOwnerType.All)]
public struct EntityVision : IComponentData
{
    [GhostField]
    public int VisionRadiusCells;
}
[GhostComponent(OwnerSendType = SendToOwnerType.All)]
public struct EntityHealth : IComponentData
{
    [GhostField]
    public float CurrentHP;
    [GhostField]
    public float MaxHP;
}
// Effective gameplay stats initialized from the definition (runtime upgrades may modify them).

[GhostComponent(OwnerSendType = SendToOwnerType.All)]
public struct EntityWork : IComponentData { public float Rate; public int JobCapacity; }
public struct EntityResourceCost : IBufferElementData { public ResourceType Type; public float Amount; }
public struct StorageResource : IBufferElementData { public ResourceType Type; }
public struct BuildOffer : IBufferElementData { public FixedString64Bytes DefinitionID; }
public struct PopulationAccount : ICleanupComponentData
{
    public int PlayerID;
    public int Used;
    public int Capacity;
}
