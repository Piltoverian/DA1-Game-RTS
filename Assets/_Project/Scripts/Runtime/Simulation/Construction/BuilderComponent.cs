using Unity.Entities;
using Unity.Mathematics;

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
    public bool RepairOnly;
    public Entity PlannedPrefab;
    public float3 PlannedPosition;
    public bool WaitingForPlacement;
    public Entity PlacementRequest;
    public FogKnownTarget KnownTarget;
    public bool FogApproachRequested;
}

// Pending buildings, in placement order. The active target remains in BuilderComponent.
public struct BuilderQueueElement : IBufferElementData
{
    public Entity BuildingEntity;
    public Entity Prefab;
    public float3 Position;
    public FogKnownTarget KnownTarget;
}
