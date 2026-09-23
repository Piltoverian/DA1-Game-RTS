using Unity.Entities;
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

public struct WorkerGatherData : IComponentData,IEnableableComponent
{
    public WorkerGatherState State;

    public Entity TargetNode;
    public Entity TargetDepot;

    public int Capacity;
    public float GatherRate;
    public float WorkRate;
    public float GatherFraction;
    public int CarryAmount;

    public float GatherTime;
    public float GatherTimer;
    public float StopDistanceSq;

    public ResourceType CurrentResourceType;
}

