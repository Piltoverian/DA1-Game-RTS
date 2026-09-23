using Unity.Collections;
using Unity.Entities;

public struct TechBlob : IGameBlobAsset
{
    // Copied from TechDefinition.IDs, matching tree and Civ prerequisite identities.
    public FixedString64Bytes ID;
    public BlobArray<ResourcePair> Cost;

    public FixedString64Bytes GetID() => ID;
    public FixedString64Bytes getID() => GetID();
}

public struct TechTreeBlob : IGameBlobAsset
{
    public FixedString64Bytes ID;
    public BlobArray<TechTreeNodeBlob> Nodes;

    public FixedString64Bytes GetID() => ID;
    public FixedString64Bytes getID() => GetID();
}

public struct TechTreeNodeBlob
{
    public FixedString64Bytes TechID;
    public BlobArray<FixedString64Bytes> Prerequisites;
}
