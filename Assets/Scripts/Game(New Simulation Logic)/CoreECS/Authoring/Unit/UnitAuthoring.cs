using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

class UnitAuthoring : MonoBehaviour
{
    public int playerID;
    public string unitName;
    public class Baker : Baker<UnitAuthoring>
    {
        
        public override void Bake(UnitAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new Unit
            {
                playerID = authoring.playerID
                ,
                unitName = new FixedString64Bytes(authoring.unitName)
            });
        }
    }
}
public struct Unit : IComponentData
{
    public int playerID;
    public FixedString64Bytes unitName;

    public string GetValueNormalizedString() { return unitName.ToString().Normalize(); }
}
