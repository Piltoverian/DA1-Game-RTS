using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct TwinShootAttackSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (
                     localTransform,
                     twinShootAttack,
                     target,
                     entity)
                 in SystemAPI.Query<
                         RefRW<LocalTransform>,
                         RefRW<TwinShootAttack>,
                         RefRW<Target>>()
                     .WithEntityAccess())
        {
            Entity targetEntity = target.ValueRO.targetEntity;

            bool hasValidTarget =
                targetEntity != Entity.Null &&
                SystemAPI.Exists(targetEntity) &&
                SystemAPI.HasComponent<LocalTransform>(targetEntity);

            if (!hasValidTarget)
            {
                target.ValueRW.targetEntity = Entity.Null;

                RotateBackToRest(
                    ref localTransform.ValueRW,
                    twinShootAttack.ValueRO,
                    deltaTime
                );

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
                twinShootAttack.ValueRO.AttackRange *
                twinShootAttack.ValueRO.AttackRange;

            if (distanceSq > attackRangeSq)
            {
                RotateBackToRest(
                    ref localTransform.ValueRW,
                    twinShootAttack.ValueRO,
                    deltaTime
                );

                continue;
            }

            RotateTowardTarget(
                ref localTransform.ValueRW,
                twinShootAttack.ValueRO,
                toTarget,
                deltaTime
            );
            float3 currentForward = math.mul(localTransform.ValueRO.Rotation, math.forward());
            float3 targetDirection = math.normalize(toTarget);

            // Dot product = 1 là ngắm chính xác 100%. 
            // 0.98f cho phép dung sai khoảng ~11 độ. Bạn có thể tùy chỉnh độ khắt khe của góc bắn.
            if (math.dot(currentForward, targetDirection) < 0.98f)
            {
                // Chưa ngắm trúng mục tiêu -> Bỏ qua phần xả đạn
                continue;
            }
            twinShootAttack.ValueRW.CooldownTimer -= deltaTime;

            if (twinShootAttack.ValueRO.CooldownTimer > 0f)
                continue;

            twinShootAttack.ValueRW.CooldownTimer =
                twinShootAttack.ValueRO.Cooldown;

            if (twinShootAttack.ValueRO.BulletPrefab == Entity.Null)
                continue;

            SpawnBullet(
                ref state,
                localTransform.ValueRO,
                twinShootAttack.ValueRO,
                targetEntity,
                twinShootAttack.ValueRO.LeftMuzzleLocalOffset
            );

            SpawnBullet(
                ref state,
                localTransform.ValueRO,
                twinShootAttack.ValueRO,
                targetEntity,
                twinShootAttack.ValueRO.RightMuzzleLocalOffset
            );
        }
    }

    private void RotateTowardTarget(
        ref LocalTransform transform,
        TwinShootAttack attack,
        float3 toTarget,
        float deltaTime)
    {
        if (math.lengthsq(toTarget) < 0.001f)
            return;

        quaternion targetRotation =
            quaternion.LookRotationSafe(
                math.normalize(toTarget),
                math.up()
            );

        float t = math.saturate(attack.RotateSpeed * deltaTime);

        transform.Rotation =
            math.slerp(transform.Rotation, targetRotation, t);
    }

    private void RotateBackToRest(
        ref LocalTransform transform,
        TwinShootAttack attack,
        float deltaTime)
    {
        float t = math.saturate(attack.RotateSpeed * deltaTime);

        transform.Rotation =
            math.slerp(transform.Rotation, attack.RestRotation, t);
    }

    private void SpawnBullet(
        ref SystemState state,
        LocalTransform towerTransform,
        TwinShootAttack twinShootAttack,
        Entity targetEntity,
        float3 muzzleLocalOffset)
    {
        Entity bulletEntity =
            state.EntityManager.Instantiate(twinShootAttack.BulletPrefab);

        float3 bulletSpawnWorldPosition =
            towerTransform.TransformPoint(muzzleLocalOffset);

        LocalTransform bulletTransform =
            SystemAPI.GetComponent<LocalTransform>(bulletEntity);

        SystemAPI.SetComponent(
            bulletEntity,
            bulletTransform.WithPosition(bulletSpawnWorldPosition)
        );

        // Kiểm tra xem Prefab đạn thuộc loại nào để gán data cho đúng
        if (SystemAPI.HasComponent<Bullet>(bulletEntity))
        {
            RefRW<Bullet> bullet =
                SystemAPI.GetComponentRW<Bullet>(bulletEntity);

            bullet.ValueRW.damage = twinShootAttack.Damage;
            bullet.ValueRW.speed = twinShootAttack.BulletSpeed;
        }
        else if (SystemAPI.HasComponent<ArtilleryBullet>(bulletEntity))
        {
            RefRW<ArtilleryBullet> artBullet =
                SystemAPI.GetComponentRW<ArtilleryBullet>(bulletEntity);

            // Tái sử dụng các biến có sẵn của trụ súng. 
            // Các chỉ số như aoeRadius hay maxHeight sẽ lấy mặc định từ lúc Bake Prefab đạn pháo.
            artBullet.ValueRW.speed = twinShootAttack.BulletSpeed;
            artBullet.ValueRW.aoeDamage = twinShootAttack.Damage;
        }

        // Gán mục tiêu chung cho cả 2 loại đạn
        RefRW<Target> bulletTarget =
            SystemAPI.GetComponentRW<Target>(bulletEntity);

        bulletTarget.ValueRW.targetEntity = targetEntity;
    }
}