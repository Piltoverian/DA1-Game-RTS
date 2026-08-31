using Unity.Entities;
using UnityEngine;
public class HouseAuthoring : UnityEngine.MonoBehaviour
{
    [SerializeField] int maxPopWillIncrease;
    class Baker : Baker<HouseAuthoring>
    {
        public override void Bake(HouseAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new HouseComponent
            {
                maxPopWillIncrease = authoring.maxPopWillIncrease
            });
            AddComponent(entity,new HouseCleanUp { maxPopWillIncrease = authoring.maxPopWillIncrease });
            AddComponent<HouseInitTag>(entity);
        }
    }
}

public struct HouseComponent : IComponentData
{
    public int maxPopWillIncrease;
}

public struct HouseCleanUp : ICleanupComponentData
{
    public int maxPopWillIncrease;
    public int playerID;
}

public struct HouseInitTag:IComponentData
{
    
}