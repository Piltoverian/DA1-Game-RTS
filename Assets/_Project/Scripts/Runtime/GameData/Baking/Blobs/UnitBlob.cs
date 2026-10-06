using Unity.Collections;
using Unity.Entities;

public struct UnitBlob : IGameBlobAsset
{
    public BaseBlob Base;
    public int PopulationCost;
    public BlobArray<ResourcePair> ResourceCosts;
    public BlobArray<UnitType> UnitTypes;

    public FixedString64Bytes GetID() => Base.ID;
    public FixedString64Bytes getID() => GetID();
}
