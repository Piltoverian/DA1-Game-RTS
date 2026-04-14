using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct MovementAgentDebugConfig : IComponentData
{
    public bool ShowVelocity;
    public bool ShowDesiredVelocity;
    public bool ShowContextSteer;
    public bool ShowTargetLines;
    public bool ShowProximity;
}

public class MovementAgentDebugAuthoring : MonoBehaviour
{
    public bool showVelocity = true;
    public bool showDesiredVelocity = true;
    public bool showContextSteer = true;
    public bool showTargetLines = true;
    public bool showProximity = true;

    class Baker : Baker<MovementAgentDebugAuthoring>
    {
        public override void Bake(MovementAgentDebugAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new MovementAgentDebugConfig
            {
                ShowVelocity = authoring.showVelocity,
                ShowDesiredVelocity = authoring.showDesiredVelocity,
                ShowContextSteer = authoring.showContextSteer,
                ShowTargetLines = authoring.showTargetLines,
                ShowProximity = authoring.showProximity
            });
        }
    }
}
