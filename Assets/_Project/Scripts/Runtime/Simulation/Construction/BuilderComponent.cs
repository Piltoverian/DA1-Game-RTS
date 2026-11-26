using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
public enum BuilderState
{
    Idle,
    GoingToSite,
    Building
}
[GhostComponent(OwnerSendType = SendToOwnerType.All)]
[GhostEnabledBit]
public struct BuilderComponent : IComponentData,IEnableableComponent
{
    [GhostField]
    public BuilderState State;
    [GhostField]
    public Entity TargetConstructionSite;
    public float BuildWorkLoadPerSecond;
    public float BuildRange;
    public float PreviousStoppingDistance;
    public bool HasApproachStoppingDistance;
    [GhostField]
    public bool RepairOnly;
    public Entity PlannedPrefab;
    public Entity NetworkBuildOrder;
    public float3 PlannedPosition;
    public bool WaitingForPlacement;
    public Entity PlacementRequest;
    public FogKnownTarget KnownTarget;
    public bool FogApproachRequested;
}

// Pending buildings, in placement order. The active target remains in BuilderComponent.
public struct BuilderQueueElement : IBufferElementData
{
    public Entity NetworkBuildOrder;
    public Entity BuildingEntity;
    public Entity Prefab;
    public float3 Position;
    public FogKnownTarget KnownTarget;
}
