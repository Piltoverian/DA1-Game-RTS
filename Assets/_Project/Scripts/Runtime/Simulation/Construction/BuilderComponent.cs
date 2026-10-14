using Unity.Entities;

public enum BuilderState
{
    Idle,
    GoingToSite,
    Building
}

public struct BuilderComponent : IComponentData,IEnableableComponent
{
    public BuilderState State;
    public Entity TargetConstructionSite;
    public float BuildWorkLoadPerSecond;
    public float BuildRange;
    public float PreviousStoppingDistance;
    public bool HasApproachStoppingDistance;
}

// Pending buildings, in placement order. The active target remains in BuilderComponent.
public struct BuilderQueueElement : IBufferElementData
{
    public Entity BuildingEntity;
}
