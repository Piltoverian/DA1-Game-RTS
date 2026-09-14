using UnityEngine;
using Unity.Entities;
public class BuilderAuthoring : MonoBehaviour
{
    [SerializeField] private float buildWorkLoadPerSecond = 10f;
    [SerializeField] private float buildRange = 2f;

    class Baker : Baker<BuilderAuthoring>
    {
        public override void Bake(BuilderAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new BuilderComponent
            {
                BuildWorkLoadPerSecond = authoring.buildWorkLoadPerSecond,
                BuildRange = authoring.buildRange,
                State = BuilderState.Idle,
                TargetConstructionSite = Entity.Null
            });
            AddBuffer<BuilderQueueElement>(entity);
            SetComponentEnabled<BuilderComponent>(entity, false);
        }
    }
}
