using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
public struct BuildingComponent : IComponentData
{
    public FixedString64Bytes DefinitionID;
    public float WorkLoad;
    public int PopulationCapacity;
}
public struct BuildingTagElement : IBufferElementData { public BuildingTags Value; }
public enum ConstructionPhase : byte { Planned, UnderConstruction, Completed, Destroyed }
[GhostComponent(OwnerSendType = SendToOwnerType.All)]
public struct BuildingConstruction : IComponentData
{
    [GhostField]
    public float CompletedWork;
    [GhostField]
    public ConstructionPhase Phase;
}
