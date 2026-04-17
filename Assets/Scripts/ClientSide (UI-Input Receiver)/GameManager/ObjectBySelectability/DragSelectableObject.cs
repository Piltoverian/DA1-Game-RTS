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
                playerID = authoring.playerID,
                GridIndex = -1//new Unit
            });
            AddComponent(entity,new Selected
            {
                visualEntity = GetEntity(authoring.visualGameObject, TransformUsageFlags.Dynamic),
                showScale = authoring.showScale
            });
            SetComponentEnabled<Selected>(entity, false);   
        }
    }
}
