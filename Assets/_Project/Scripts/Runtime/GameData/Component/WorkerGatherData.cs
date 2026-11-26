using Unity.Entities;
using Unity.NetCode;
public struct WorkerTag : IComponentData
{
}

public enum WorkerGatherState
{
    Idle,
    GoingToNode,
    Gathering,
    ReturningDepot
}

[GhostComponent(OwnerSendType = SendToOwnerType.All)]
[GhostEnabledBit]
public struct WorkerGatherData : IComponentData,IEnableableComponent
{
    [GhostField]
    public WorkerGatherState State;
    public Entity TargetNode;
    public Entity TargetDepot;


    public int Capacity;
    public float GatherRate;
    public float WorkRate;
    public float GatherFraction;
    [GhostField]
    public int CarryAmount;

    public float GatherTime;
    public float GatherTimer;
    public float StopDistanceSq;

    [GhostField]
    public ResourceType CurrentResourceType;
    public FogKnownTarget KnownTarget;
}

