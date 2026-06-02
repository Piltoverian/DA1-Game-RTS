using Unity.Entities;
using UnityEngine;

public class BulletAuthoring : MonoBehaviour
{
    public float speed;
    public float damage;
    public class Baker : Baker<BulletAuthoring>
    {
        public override void Bake(BulletAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new Bullet
            {
                speed = authoring.speed,
                damage = authoring.damage,
            });
            foreach (Transform child in authoring.transform)
            {
                GetEntity(child.gameObject, TransformUsageFlags.Dynamic);
            }
        }
    }
}
public struct Bullet : IComponentData
{
    public float speed;
    public float damage;
}