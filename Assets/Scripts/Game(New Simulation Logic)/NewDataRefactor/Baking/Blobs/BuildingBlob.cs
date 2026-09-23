using Unity.Collections;
using Unity.Entities;

public struct BuildingBlob : IGameBlobAsset
{
    public BaseBlob Base;
    public BlobArray<BuildingTags> Tags;
    public BlobArray<ResourcePair> Cost;
    public int PopulationCapacity;
    public float WorkLoad;

    public FixedString64Bytes GetID() => Base.ID;
    public FixedString64Bytes getID() => GetID();
}
