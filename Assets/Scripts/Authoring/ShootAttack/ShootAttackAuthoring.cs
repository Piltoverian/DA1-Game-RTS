using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class ShootAttackAuthoring : MonoBehaviour
{
    public float timerMax;
    public float damage;
    public float attackDistance;
    public Transform bulletSpawnPos;
    public GameObject bulletPrefab;

    public class Baker : Baker<ShootAttackAuthoring>
    {
        
        public override void Bake(ShootAttackAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new ShootAttack {
                bulletSpawnPos = authoring.bulletSpawnPos.localPosition,
                timerMax = authoring.timerMax,
                damage = authoring.damage,
                attackDistance = authoring.attackDistance,
                bulletPrefab = GetEntity(authoring.bulletPrefab, TransformUsageFlags.Dynamic)
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
    public float3 bulletSpawnPos;
    public Entity bulletPrefab;
}