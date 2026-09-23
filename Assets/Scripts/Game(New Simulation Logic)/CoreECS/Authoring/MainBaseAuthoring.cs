using Unity.Entities;
using UnityEngine;


public class MainBaseAuthoring : MonoBehaviour
{
    public class Baker : Baker<MainBaseAuthoring>
    {
        public override void Bake(MainBaseAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<MainBaseTag>(entity);
        }
    }
}

public struct MainBaseTag : IComponentData
{
}
