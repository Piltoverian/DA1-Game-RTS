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
}
