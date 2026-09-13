using Unity.Entities;
using Unity.Mathematics;

public struct PlaceBuildingRequest : IComponentData
{
    public int PlayerId;
    public Entity PrefabEntity;
    public float3 Position;
    public float TotalWorkLoad;
}

public struct PlaceBuildingWorkerElement : IBufferElementData
{
    public Entity WorkerEntity;
}

public struct CancelBuildingRequest : IComponentData
{
    public Entity BuildingEntity;
    public int PlayerId;
}
