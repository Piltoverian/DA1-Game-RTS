using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using static TowerAttackAuthoring;

public partial struct TowerAttackSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (localTransform, towerAttack, target, weaponBuffer)
                 in SystemAPI.Query<RefRW<LocalTransform>, RefRW<TowerAttack>, RefRW<Target>, DynamicBuffer<WeaponSlot>>())
        {
            Entity targetEntity = target.ValueRO.targetEntity;

            bool hasValidTarget = targetEntity != Entity.Null &&
                                  SystemAPI.Exists(targetEntity) &&
                                  SystemAPI.HasComponent<LocalTransform>(targetEntity);

            if (!hasValidTarget)
            {
                target.ValueRW.targetEntity = Entity.Null;
                RotateBackToRest(ref localTransform.ValueRW, towerAttack.ValueRO, deltaTime);
                continue;
            }

            LocalTransform targetTransform = SystemAPI.GetComponent<LocalTransform>(targetEntity);
            float3 towerPos = localTransform.ValueRO.Position;
            float3 targetPos = targetTransform.Position;

            float3 toTarget = targetPos - towerPos;
            toTarget.y = 0f;

            float distanceSq = math.lengthsq(toTarget);
            float attackRangeSq = towerAttack.ValueRO.AttackRange * towerAttack.ValueRO.AttackRange;

            if (distanceSq > attackRangeSq)
            {
                RotateBackToRest(ref localTransform.ValueRW, towerAttack.ValueRO, deltaTime);
                continue;
            }

            RotateTowardTarget(ref localTransform.ValueRW, towerAttack.ValueRO, toTarget, deltaTime);

            float3 currentForward = math.mul(localTransform.ValueRO.Rotation, math.forward());
            float3 targetDirection = math.normalize(toTarget);

            if (math.dot(currentForward, targetDirection) < 0.98f)
            {
                continue; 
            }

            for (int i = 0; i < weaponBuffer.Length; i++)
            {
                var weapon = weaponBuffer[i];
                weapon.CooldownTimer -= deltaTime;

                if (weapon.CooldownTimer <= 0f)
                {
                    weapon.CooldownTimer = weapon.Cooldown;

                    if (weapon.BulletPrefab != Entity.Null)
                    {
                        SpawnBullet(ref state, localTransform.ValueRO, weapon, targetEntity);
                    }
                }

                var buffer = weaponBuffer;

                for (int weaponIndex = 0; weaponIndex < buffer.Length; weaponIndex++)
                {
                    ref WeaponSlot currentWeapon = ref buffer.ElementAt(weaponIndex);

                    currentWeapon.CooldownTimer -= deltaTime;

                    if (currentWeapon.CooldownTimer <= 0f)
                    {
                        currentWeapon.CooldownTimer = currentWeapon.Cooldown;

                        if (currentWeapon.BulletPrefab != Entity.Null)
                        {
                            SpawnBullet(ref state, localTransform.ValueRO, currentWeapon, targetEntity);
                        }
                    }
                }
            }
        }
    }

    private void RotateTowardTarget(ref LocalTransform transform, TowerAttack attack, float3 toTarget, float deltaTime)
    {
        if (math.lengthsq(toTarget) < 0.001f) return;
        quaternion targetRotation = quaternion.LookRotationSafe(math.normalize(toTarget), math.up());
        float t = math.saturate(attack.RotateSpeed * deltaTime);
        transform.Rotation = math.slerp(transform.Rotation, targetRotation, t);
    }

    private void RotateBackToRest(ref LocalTransform transform, TowerAttack attack, float deltaTime)
    {
        float t = math.saturate(attack.RotateSpeed * deltaTime);
        transform.Rotation = math.slerp(transform.Rotation, attack.RestRotation, t);
    }

    private void SpawnBullet(ref SystemState state, LocalTransform towerTransform, WeaponSlot weapon, Entity targetEntity)
    {
        Entity bulletEntity = state.EntityManager.Instantiate(weapon.BulletPrefab);

        float3 bulletSpawnWorldPosition = towerTransform.TransformPoint(weapon.MuzzleLocalOffset);

        LocalTransform bulletTransform = SystemAPI.GetComponent<LocalTransform>(bulletEntity);
        SystemAPI.SetComponent(bulletEntity, bulletTransform.WithPosition(bulletSpawnWorldPosition));

        if (SystemAPI.HasComponent<Bullet>(bulletEntity))
        {
            RefRW<Bullet> bullet = SystemAPI.GetComponentRW<Bullet>(bulletEntity);
            bullet.ValueRW.damage = weapon.Damage;
            bullet.ValueRW.speed = weapon.BulletSpeed;
        }
        else if (SystemAPI.HasComponent<ArtilleryBullet>(bulletEntity))
        {
            RefRW<ArtilleryBullet> artBullet = SystemAPI.GetComponentRW<ArtilleryBullet>(bulletEntity);
            artBullet.ValueRW.speed = weapon.BulletSpeed;
            artBullet.ValueRW.aoeDamage = weapon.Damage;
        }

        RefRW<Target> bulletTarget = SystemAPI.GetComponentRW<Target>(bulletEntity);
        bulletTarget.ValueRW.targetEntity = targetEntity;
    }
}