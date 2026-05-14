using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class MoveOverrideAuthoring : MonoBehaviour
{
    public float stopDistance = 3.0f; 

    class Baker : Baker<MoveOverrideAuthoring>
    {
        public override void Bake(MoveOverrideAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new MoveOverride
            {
                stopDistanceSq = authoring.stopDistance* authoring.stopDistance,
                targetApplied = false
            });
        }
    }
}
public struct MoveOverride : IComponentData, IEnableableComponent
{
    public float3 targetPosition;
    public bool targetApplied;

    public float stopDistanceSq; 
}