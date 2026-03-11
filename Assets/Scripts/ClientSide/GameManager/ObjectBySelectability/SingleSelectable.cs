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
                playerID = 1,
                GridIndex = -1//new Unit
            });
        }
    }
}
