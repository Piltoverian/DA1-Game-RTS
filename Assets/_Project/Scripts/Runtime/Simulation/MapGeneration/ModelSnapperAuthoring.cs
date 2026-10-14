using Unity.Entities;
using UnityEngine;

[DisallowMultipleComponent]
public class ModelSnapperAuthoring : MonoBehaviour
{
    [Min(1),Tooltip("Fallback size in cells when the entity has no movement agent. An existing GridSnapperAuthoring takes precedence.")]
    public int GridScale=1;
    class Baker : Baker<ModelSnapperAuthoring>
    {
        public override void Bake(ModelSnapperAuthoring authoring)
        {
            var agent=GetComponent<MovementAgentAuthoring>();
            var footprint=GetComponent<GridSnapperAuthoring>();
            int cells=footprint?footprint.GridScale:authoring.GridScale;
            var entity=GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity,ModelSnapSource.Capture(authoring.gameObject,cells,agent?agent.diameterCells:0));
            AddComponent<Snapping>(entity);
            if(!agent&&!footprint)AddComponent(entity,new GridFootprint{Cells=cells});
        }
    }
}
