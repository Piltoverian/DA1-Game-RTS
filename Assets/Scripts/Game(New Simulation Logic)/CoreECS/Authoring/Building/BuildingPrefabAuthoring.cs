using Unity.Entities;
using UnityEngine;

public class BuildingPrefabAuthoring : MonoBehaviour
{
    public GameObject BarracksPrefab;
    public GameObject TowerPrefab;
    public GameObject ResourceDepotPrefab;

    class Baker : Baker<BuildingPrefabAuthoring>
    {
        public override void Bake(BuildingPrefabAuthoring src)
        {
            Entity e = GetEntity(TransformUsageFlags.None);

            AddComponent(e, new BuildingPrefabData
            {
                BarracksPrefab = GetEntity(src.BarracksPrefab, TransformUsageFlags.Dynamic),
                TowerPrefab = GetEntity(src.TowerPrefab, TransformUsageFlags.Dynamic),
                ResourceDepotPrefab = GetEntity(src.ResourceDepotPrefab, TransformUsageFlags.Dynamic)
            });
        }
    }
}

public struct BuildingPrefabData : IComponentData
{
    public Entity BarracksPrefab;
    public Entity TowerPrefab;
    public Entity ResourceDepotPrefab;
}
