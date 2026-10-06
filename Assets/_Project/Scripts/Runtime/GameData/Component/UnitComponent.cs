using Unity.Collections;
using Unity.Entities;

public struct UnitComponent : IComponentData
{
    public FixedString64Bytes DefinitionID;
    public int PopulationCost;
}
public struct UnitTypeElement : IBufferElementData { public UnitType Value; }
