using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class TwinShootAttackAuthoring : MonoBehaviour
{
    public GameObject bulletPrefab;

    public float attackRange = 20f;
    public float damage = 10f;
    public float cooldown = 1f;
    public float bulletSpeed = 25f;

    public Transform leftMuzzle;
    public Transform rightMuzzle;

    public float rotateSpeed = 8f;

    public class Baker : Baker<TwinShootAttackAuthoring>
    {
        public override void Bake(TwinShootAttackAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            Entity bulletEntity = Entity.Null;

            if (authoring.bulletPrefab != null)
            {
                bulletEntity = GetEntity(
                    authoring.bulletPrefab,
                    TransformUsageFlags.Dynamic
                );
            }

            float3 leftOffset = float3.zero;
            float3 rightOffset = float3.zero;

            if (authoring.leftMuzzle != null)
            {
                leftOffset = authoring.transform.InverseTransformPoint(
                    authoring.leftMuzzle.position
                );
            }

            if (authoring.rightMuzzle != null)
            {
                rightOffset = authoring.transform.InverseTransformPoint(
                    authoring.rightMuzzle.position
                );
            }

            AddComponent(entity, new TwinShootAttack
            {
                BulletPrefab = bulletEntity,

                AttackRange = authoring.attackRange,
                Damage = authoring.damage,
                Cooldown = authoring.cooldown,
                CooldownTimer = 0f,

                BulletSpeed = authoring.bulletSpeed,

                LeftMuzzleLocalOffset = leftOffset,
                RightMuzzleLocalOffset = rightOffset,

                RotateSpeed = authoring.rotateSpeed,
                RestRotation = authoring.transform.rotation
            });
        }
    }
}
public struct TwinShootAttack : IComponentData
{
    public Entity BulletPrefab;

    public float AttackRange;
    public float Damage;
    public float Cooldown;
    public float CooldownTimer;

    public float BulletSpeed;

    public float3 LeftMuzzleLocalOffset;
    public float3 RightMuzzleLocalOffset;

    public float RotateSpeed;
    public quaternion RestRotation;
}