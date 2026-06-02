using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class MoveOverrideAuthoring : MonoBehaviour
{
    class Baker : Baker<MoveOverrideAuthoring>
    {
        public override void Bake(MoveOverrideAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new MoveOverride
            {
                targetPosition = float3.zero,
                targetApplied = false
            });

            SetComponentEnabled<MoveOverride>(entity, false);
        }
    }
}
public struct MoveOverride : IComponentData, IEnableableComponent
{
    public float3 targetPosition;
    public bool targetApplied;
}