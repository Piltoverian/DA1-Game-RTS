using Unity.Entities;
using UnityEngine;

public class ShootAttackAuthoring : MonoBehaviour
{
    public float timerMax;
    public float damage;
    public float attackDistance;
    public GameObject bulletSpawnPos;

    public class Baker : Baker<ShootAttackAuthoring>
    {
        
        public override void Bake(ShootAttackAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new ShootAttack {
                bulletSpawnPos = GetEntity(authoring.bulletSpawnPos, TransformUsageFlags.Dynamic),
                timerMax = authoring.timerMax,
                damage = authoring.damage,
                attackDistance = authoring.attackDistance
            });
        }
    }
}
public struct ShootAttack : IComponentData
{
    public float timer;
    public float timerMax;
    public float damage;
    public float attackDistance;
    public Entity bulletSpawnPos;
}