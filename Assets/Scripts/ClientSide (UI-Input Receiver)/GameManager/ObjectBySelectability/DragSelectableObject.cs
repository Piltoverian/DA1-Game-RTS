using Unity.Entities;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

public class DragSelectableObject : SelectableObject
{
    class UnitBaker : Baker<DragSelectableObject>
    {
        public override void Bake(DragSelectableObject authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new DragSelectableEntity());
            AddComponent(entity, new Selectable
            {
                playerID = 1,
                GridIndex = -1//new Unit
            });
            
        }
    }
}
