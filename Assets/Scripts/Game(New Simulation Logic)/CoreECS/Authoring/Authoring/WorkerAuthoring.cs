using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class WorkerAuthoring : MonoBehaviour
{
    public int Capacity = 10;
    public float GatherTime = 2f;
    public float StopDistance = 1.5f;

    class Baker : Baker<WorkerAuthoring>
    {
        public override void Bake(WorkerAuthoring src)
        {
            Entity e = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent<WorkerTag>(e);

            AddComponent(e, new WorkerGatherData
            {
                State = WorkerGatherState.GoingToNode,

                TargetNode = Entity.Null,
                TargetDepot = Entity.Null,

                Capacity = src.Capacity,
                CarryAmount = 0,

                GatherTime = src.GatherTime,
                GatherTimer = 0f,

                StopDistanceSq =
                    src.StopDistance * src.StopDistance,

                CurrentResourceType = ResourceType.Gold
            });
        }
    }
}

public struct WorkerTag : IComponentData
{
}

public enum WorkerGatherState
{
    GoingToNode,
    Gathering,
    ReturningDepot
}

public struct WorkerGatherData : IComponentData
{
    public WorkerGatherState State;

    public Entity TargetNode;
    public Entity TargetDepot;

    public int Capacity;
    public int CarryAmount;

    public float GatherTime;
    public float GatherTimer;
    public float StopDistanceSq;

    public ResourceType CurrentResourceType;
}
