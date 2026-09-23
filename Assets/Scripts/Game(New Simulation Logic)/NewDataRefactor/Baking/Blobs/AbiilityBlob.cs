using Unity.Collections;
using Unity.Entities;

public struct AttackBlob : IGameBlobAsset
{
    public FixedString64Bytes ID;
    public float Range;
    public float RotationSpeed;

    public BlobArray<WeaponDefinitionBlob> Weapons;

    public FixedString64Bytes GetID() => ID;

    public FixedString64Bytes getID()
    {
        return ID;
    }
}

public struct BuildBlob : IGameBlobAsset
{
    public FixedString64Bytes ID;
    public float Range;
    public BlobArray<FixedString64Bytes> BuildingIDsInReg;
    public FixedString64Bytes GetID() => ID;

    public FixedString64Bytes getID()
    {
        return ID;
    }
}

public struct GatherBlob : IGameBlobAsset
{
    public FixedString64Bytes ID;
    public int Capacity;
    public float GatherTime;
    public float StopDistance;
    public float GatherRate;
    public FixedString64Bytes GetID() => ID;

    public FixedString64Bytes getID()
    {
        return ID;
    }
}

public struct StorageBlob : IGameBlobAsset
{
    public FixedString64Bytes ID;
    public BlobArray<ResourceType> Resources;
    public FixedString64Bytes GetID() => ID;

    public FixedString64Bytes getID()
    {
        return ID;
    }
}
