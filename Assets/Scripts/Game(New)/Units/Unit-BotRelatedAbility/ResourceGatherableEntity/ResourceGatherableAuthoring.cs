using Unity.Entities;
using UnityEngine;

public struct ResourceGatherableComp : IComponentData
{
    public enum GatheringState
    {
        Idle,
        MovingToNode,
        Gathering,
        ReturningToBase
    }
    public float GatheringSpeed;
    public float gatheringRange;
    public float currentlyCarryingAmount;
    public ResourceType resourceType;
    public GatheringState gatheringState;
}

public class ResourceGatherableAuthoring : MonoBehaviour
{
    public float GatheringSpeed;
    public float gatheringRange;
    public float currentlyCarryingAmount;
    ResourceNode currentResourceNode=null;
    public ResourceType resourceType=ResourceType.None;
    public ResourceGatherableComp.GatheringState gatheringState= ResourceGatherableComp.GatheringState.Idle;
    Storage targetStorage=null;

    public class Baker : Baker<ResourceGatherableAuthoring>
    {
        public override void Bake(ResourceGatherableAuthoring authoring)
        {
            var entity=GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity,new ResourceGatherableComp
            {
                GatheringSpeed = authoring.GatheringSpeed,
                gatheringRange = authoring.gatheringRange,
                resourceType=authoring.resourceType,
                gatheringState=authoring.gatheringState,
                currentlyCarryingAmount=authoring.currentlyCarryingAmount
            });
        }
    }
}
