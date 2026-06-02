using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct UnitWeaponConfig
{
    public GameObject bulletPrefab;
    public Transform bulletSpawnPos;
    public float timerMax;
    public float damage;
    public float bulletSpeed;
}

public class ShootAttackAuthoring : MonoBehaviour
{
    public float attackDistance = 10f;
    public List<UnitWeaponConfig> weapons;

    public class Baker : Baker<ShootAttackAuthoring>
    {
        public override void Bake(ShootAttackAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // 1. Thêm data chung
            AddComponent(entity, new ShootAttack
            {
                attackDistance = authoring.attackDistance
            });

            // 2. Thêm Buffer vũ khí
            DynamicBuffer<UnitWeaponSlot> weaponBuffer = AddBuffer<UnitWeaponSlot>(entity);

            foreach (var config in authoring.weapons)
            {
                Entity bulletEntity = Entity.Null;
                if (config.bulletPrefab != null)
                {
                    bulletEntity = GetEntity(config.bulletPrefab, TransformUsageFlags.Dynamic);
                }

                // Tính offset từ tâm Unit đến nòng súng
                float3 offset = float3.zero;
                if (config.bulletSpawnPos != null)
                {
                    offset = authoring.transform.InverseTransformPoint(config.bulletSpawnPos.position);
                }

                weaponBuffer.Add(new UnitWeaponSlot
                {
                    bulletPrefab = bulletEntity,
                    bulletSpawnLocalPos = offset,
                    timerMax = config.timerMax,
                    timer = 0f, // Sẵn sàng bắn ngay
                    damage = config.damage,
                    bulletSpeed = config.bulletSpeed
                });
            }
        }
    }
}
public struct ShootAttack : IComponentData
{
    public float attackDistance;
}

[InternalBufferCapacity(4)]
public struct UnitWeaponSlot : IBufferElementData
{
    public Entity bulletPrefab;
    public float3 bulletSpawnLocalPos;

    public float timer;
    public float timerMax;

    public float damage;
    public float bulletSpeed;
}