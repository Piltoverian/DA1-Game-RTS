using Unity.Entities;
using UnityEngine;

public class SingleSelectable : SelectableObject
{
    public class Baker : Baker<SingleSelectable>
    {
        public override void Bake(SingleSelectable authoring)
        {
            Entity entity=GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new SingleSelectableEntity());
            AddComponent(entity, new Selectable
            {
                playerID = authoring.playerID,
                GridIndex = -1//new Unit
            });
            AddComponent(entity, new Selected
            {
                visualEntity = GetEntity(authoring.visualGameObject, TransformUsageFlags.Dynamic),
                showScale = authoring.showScale
            });
            SetComponentEnabled<Selected>(entity, false);
        }
    }
}
