using Unity.Entities;
using UnityEngine;

public struct SetupUnitMoverDefaultPosition : IComponentData
{
}

public class SetupUnitMoverDefaultPositionAuthoring : MonoBehaviour
{
    class Baker : Baker<SetupUnitMoverDefaultPositionAuthoring>
    {
        public override void Bake(
            SetupUnitMoverDefaultPositionAuthoring authoring)
        {
            Entity entity =
                GetEntity(TransformUsageFlags.Dynamic);

            AddComponent<SetupUnitMoverDefaultPosition>(entity);
        }
    }
}
