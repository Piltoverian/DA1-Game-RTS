using Unity.Entities;
using UnityEngine;

public class EntitiesReferencesAuthoring : MonoBehaviour
{
    public GameObject bulletPrefab;
    public GameObject enemyPrefab;
    public class Baker : Baker<EntitiesReferencesAuthoring>
    {
        public override void Bake(EntitiesReferencesAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new EntitiesReferences
            {
                bulletPrefab = GetEntity(authoring.bulletPrefab, TransformUsageFlags.Dynamic),
                enemyPrefab = GetEntity(authoring.enemyPrefab, TransformUsageFlags.Dynamic)
            });
        }
    }
}
public struct EntitiesReferences : IComponentData
{
    public Entity bulletPrefab;
    public Entity enemyPrefab;
}