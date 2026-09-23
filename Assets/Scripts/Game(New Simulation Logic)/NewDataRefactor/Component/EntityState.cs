using Unity.Collections;
using Unity.Entities;

public struct EntityOwner : IComponentData { public int PlayerID; }
public struct EntityHealth : IComponentData
{
    public float CurrentHP;
    public float MaxHP;
    public bool Changed;
}
// Effective gameplay stats initialized from the definition (runtime upgrades may modify them).
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
