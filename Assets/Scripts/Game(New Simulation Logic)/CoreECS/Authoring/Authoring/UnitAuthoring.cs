using Unity.Entities;
using UnityEngine;

class UnitAuthoring : MonoBehaviour
{
    public int playerID;
    public int maxHealth;
    public class Baker : Baker<UnitAuthoring>
    {
        
        public override void Bake(UnitAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new Unit
            {
                playerID = authoring.playerID,
                maxHealth = authoring.maxHealth
            });
        }
    }
}
public struct Unit : IComponentData
{
    public int playerID;
    public int maxHealth;
}
