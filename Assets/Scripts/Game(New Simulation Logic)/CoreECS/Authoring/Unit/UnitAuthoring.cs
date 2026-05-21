using Unity.Entities;
using UnityEngine;

class UnitAuthoring : MonoBehaviour
{
    public int playerID;
    public class Baker : Baker<UnitAuthoring>
    {
        
        public override void Bake(UnitAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new Unit
            {
                playerID = authoring.playerID
            });
        }
    }
}
public struct Unit : IComponentData
{
    public int playerID;
}
