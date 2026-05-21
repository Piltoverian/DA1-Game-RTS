using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct WeaponConfig
{
    public GameObject bulletPrefab;
    public Transform muzzleTransform; 
    public float cooldown;
    public float damage;
    public float bulletSpeed;
}

public class TowerAttackAuthoring : MonoBehaviour
{
    [Header("Tower Settings")]
    public float attackRange = 20f;
    public float rotateSpeed = 8f;

    [Header("Weapons Setup")]
    public List<WeaponConfig> weapons; 

    public class Baker : Baker<TowerAttackAuthoring>
    {
        public override void Bake(TowerAttackAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new TowerAttack
            {
                AttackRange = authoring.attackRange,
                RotateSpeed = authoring.rotateSpeed,
                RestRotation = authoring.transform.rotation
            });

            DynamicBuffer<WeaponSlot> weaponBuffer = AddBuffer<WeaponSlot>(entity);

            foreach (var config in authoring.weapons)
            {
                Entity bulletEntity = Entity.Null;
                if (config.bulletPrefab != null)
                {
                    bulletEntity = GetEntity(config.bulletPrefab, TransformUsageFlags.Dynamic);
                }

                float3 offset = float3.zero;
                if (config.muzzleTransform != null)
                {
                    offset = authoring.transform.InverseTransformPoint(config.muzzleTransform.position);
                }

                weaponBuffer.Add(new WeaponSlot
                {
                    BulletPrefab = bulletEntity,
                    MuzzleLocalOffset = offset,
                    Cooldown = config.cooldown,
                    CooldownTimer = 0f, 
                    Damage = config.damage,
                    BulletSpeed = config.bulletSpeed
                });
            }
        }
    }
    public struct TowerAttack : IComponentData
    {
        public float AttackRange;
        public float RotateSpeed;
        public quaternion RestRotation;
    }

    [InternalBufferCapacity(4)] 
    public struct WeaponSlot : IBufferElementData
    {
        public Entity BulletPrefab;
        public float3 MuzzleLocalOffset;

        public float Cooldown;
        public float CooldownTimer;

        public float Damage;
        public float BulletSpeed;
    }
}