using Unity.Collections;
using Unity.Entities;

public struct CivBlob : IGameBlobAsset
{
    public FixedString64Bytes ID;
    public FixedString64Bytes TechTreeID;
    public BlobArray<CivUnitUnlockBlob> UnitUnlocks;

    public FixedString64Bytes GetID() => ID;
    public FixedString64Bytes getID() => GetID();
}

public struct CivUnitUnlockBlob
{
    public FixedString64Bytes UnitID;
    public BlobArray<FixedString64Bytes> Prerequisites;
}
