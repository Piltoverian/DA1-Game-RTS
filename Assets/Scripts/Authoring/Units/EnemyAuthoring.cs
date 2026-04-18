using UnityEngine;
using Unity.Entities;
public class EnemyAuthoring : MonoBehaviour
{
    public class EnemyBaker : Baker<EnemyAuthoring>
    {
        public override void Bake(EnemyAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new Enemy());
        }
    }
}
struct Enemy : IComponentData
{
    // You can add fields here to store enemy-specific data, such as health, speed, etc.
}
