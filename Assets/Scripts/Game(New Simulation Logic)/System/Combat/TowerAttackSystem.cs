using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using static TowerAttackAuthoring;

public partial struct TowerAttackSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        ComponentLookup<UnderConstructionTag> underConstructionLookup =
            SystemAPI.GetComponentLookup<UnderConstructionTag>(true);

        ComponentLookup<Parent> parentLookup =
            SystemAPI.GetComponentLookup<Parent>(true);

        foreach (var (localTransform, towerAttack, target, weaponBuffer, entity)
                 in SystemAPI.Query<
                         RefRW<LocalTransform>,
                         RefRO<TowerAttack>,
                         RefRW<Target>,
                         DynamicBuffer<WeaponSlot>>()
                     .WithEntityAccess())
        {
            if (IsUnderConstruction(entity, underConstructionLookup, parentLookup))
            {
                target.ValueRW.targetEntity = Entity.Null;
                RotateBackToRest(ref localTransform.ValueRW, towerAttack.ValueRO, deltaTime);
                continue;
            }

            Entity targetEntity = target.ValueRO.targetEntity;

            bool hasValidTarget =
                targetEntity != Entity.Null &&
                SystemAPI.Exists(targetEntity) &&
                SystemAPI.HasComponent<LocalTransform>(targetEntity) &&
                SystemAPI.HasComponent<Health>(targetEntity);

            if (!hasValidTarget)
            {
                target.ValueRW.targetEntity = Entity.Null;
                RotateBackToRest(ref localTransform.ValueRW, towerAttack.ValueRO, deltaTime);
                continue;
            }

            LocalTransform targetTransform =
                SystemAPI.GetComponent<LocalTransform>(targetEntity);

            float3 towerPos = localTransform.ValueRO.Position;
            float3 targetPos = targetTransform.Position;

            float3 toTarget = targetPos - towerPos;
            toTarget.y = 0f;

            float distanceSq = math.lengthsq(toTarget);
            float attackRangeSq =
                towerAttack.ValueRO.AttackRange *
                towerAttack.ValueRO.AttackRange;

            if (distanceSq > attackRangeSq)
            {
                target.ValueRW.targetEntity = Entity.Null;
                RotateBackToRest(ref localTransform.ValueRW, towerAttack.ValueRO, deltaTime);
                continue;
            }

            if (distanceSq < 0.001f)
                continue;

            RotateTowardTarget(
                ref localTransform.ValueRW,
                towerAttack.ValueRO,
                toTarget,
                deltaTime
            );

            float3 currentForward =
                math.mul(localTransform.ValueRO.Rotation, math.forward());

            float3 targetDirection =
                math.normalizesafe(toTarget);

            if (math.dot(currentForward, targetDirection) < 0.98f)
                continue;

            for (int i = 0; i < weaponBuffer.Length; i++)
            {
                ref WeaponSlot weapon = ref weaponBuffer.ElementAt(i);

                weapon.CooldownTimer -= deltaTime;

                if (weapon.CooldownTimer > 0f)
                    continue;

                weapon.CooldownTimer = weapon.Cooldown;

                if (weapon.BulletPrefab == Entity.Null)
                    continue;

                SpawnBullet(
                    ref state,
                    localTransform.ValueRO,
                    weapon,
                    targetEntity
                );
            }
        }
    }

    private bool IsUnderConstruction(
        Entity entity,
        ComponentLookup<UnderConstructionTag> underConstructionLookup,
        ComponentLookup<Parent> parentLookup)
    {
        if (underConstructionLookup.HasComponent(entity))
            return true;

        Entity current = entity;

        for (int i = 0; i < 8; i++)
        {
            if (!parentLookup.HasComponent(current))
                return false;

            current = parentLookup[current].Value;

            if (underConstructionLookup.HasComponent(current))
                return true;
        }

        return false;
    }

    private void RotateTowardTarget(
        ref LocalTransform transform,
        TowerAttack attack,
        float3 toTarget,
        float deltaTime)
    {
        if (math.lengthsq(toTarget) < 0.001f)
            return;

        quaternion targetRotation =
            quaternion.LookRotationSafe(math.normalize(toTarget), math.up());

        float t = math.saturate(attack.RotateSpeed * deltaTime);

        transform.Rotation =
            math.slerp(transform.Rotation, targetRotation, t);
    }

    private void RotateBackToRest(
        ref LocalTransform transform,
        TowerAttack attack,
        float deltaTime)
    {
        float t = math.saturate(attack.RotateSpeed * deltaTime);

        transform.Rotation =
            math.slerp(transform.Rotation, attack.RestRotation, t);
    }

    private void SpawnBullet(
        ref SystemState state,
        LocalTransform towerTransform,
        WeaponSlot weapon,
        Entity targetEntity)
    {
        Entity bulletEntity =
            state.EntityManager.Instantiate(weapon.BulletPrefab);

        float3 bulletSpawnWorldPosition =
            towerTransform.TransformPoint(weapon.MuzzleLocalOffset);

        if (SystemAPI.HasComponent<LocalTransform>(bulletEntity))
        {
            LocalTransform bulletTransform =
                SystemAPI.GetComponent<LocalTransform>(bulletEntity);

            SystemAPI.SetComponent(
                bulletEntity,
                bulletTransform.WithPosition(bulletSpawnWorldPosition)
            );
        }
        else
        {
            state.EntityManager.AddComponentData(
                bulletEntity,
                LocalTransform.FromPosition(bulletSpawnWorldPosition)
            );
        }

        if (SystemAPI.HasComponent<Bullet>(bulletEntity))
        {
            RefRW<Bullet> bullet =
                SystemAPI.GetComponentRW<Bullet>(bulletEntity);

            bullet.ValueRW.damage = weapon.Damage;
            bullet.ValueRW.speed = weapon.BulletSpeed;
        }
        else if (SystemAPI.HasComponent<ArtilleryBullet>(bulletEntity))
        {
            RefRW<ArtilleryBullet> artBullet =
                SystemAPI.GetComponentRW<ArtilleryBullet>(bulletEntity);

            artBullet.ValueRW.speed = weapon.BulletSpeed;
            artBullet.ValueRW.aoeDamage = weapon.Damage;
        }

        if (SystemAPI.HasComponent<Target>(bulletEntity))
        {
            RefRW<Target> bulletTarget =
                SystemAPI.GetComponentRW<Target>(bulletEntity);

            bulletTarget.ValueRW.targetEntity = targetEntity;
        }
        else
        {
            state.EntityManager.AddComponentData(
                bulletEntity,
                new Target
                {
                    targetEntity = targetEntity
                }
            );
        }
    }
}