using Unity.Entities;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;

public class UnitMovementAuthoring : MonoBehaviour
{
    public float speed =1.0f;

    public class Baker : Baker<UnitMovementAuthoring>
    {
        public override void Bake(UnitMovementAuthoring authoring)
        {
            Entity entity= GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitMovementComponent
            {
                speed=authoring.speed,
                hastarget=true
            });
        }
    }
}
