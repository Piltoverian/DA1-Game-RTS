using Unity.Collections;
using Unity.Entities;

public struct JobBlob : IGameBlobAsset
{
    public FixedString64Bytes ID;
    public float WorkLoad;

    public FixedString64Bytes GetID() => ID;
    public FixedString64Bytes getID() => GetID();
}

public struct TrainBlob : IGameBlobAsset
{
    public JobBlob Job;
    public FixedString64Bytes OutputUnitID;

    public FixedString64Bytes GetID() => Job.ID;
    public FixedString64Bytes getID() => GetID();
}

public struct ResearchBlob : IGameBlobAsset
{
    public JobBlob Job;
    public TechBlob Tech;

    public FixedString64Bytes GetID() => Job.ID;
    public FixedString64Bytes getID() => GetID();
}
