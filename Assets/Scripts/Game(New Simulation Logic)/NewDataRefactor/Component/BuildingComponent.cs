using Unity.Collections;
using Unity.Entities;

public struct BuildingComponent : IComponentData
{
    public FixedString64Bytes DefinitionID;
    public float WorkLoad;
    public int PopulationCapacity;
}
public struct BuildingTagElement : IBufferElementData { public BuildingTags Value; }
public enum ConstructionPhase : byte { Planned, UnderConstruction, Completed, Destroyed }
public struct BuildingConstruction : IComponentData
{
    public float CompletedWork;
    public ConstructionPhase Phase;
}
