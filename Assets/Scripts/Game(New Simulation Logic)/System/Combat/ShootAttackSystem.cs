using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateBefore(typeof(HealthDeadTestSystem))]
partial struct ShootAttackSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        GridComponent gridComponent = SystemAPI.GetSingleton<GridComponent>();
        var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (localTransform, shootAttack, target, movementAgent, weaponBuffer, unit, entity)
                 in SystemAPI.Query<
                     RefRW<LocalTransform>,
                     RefRW<ShootAttack>,
                     RefRW<Target>,
                     RefRW<MovementAgentComponent>,
                     DynamicBuffer<UnitWeaponSlot>,
                     RefRO<Unit>>().WithDisabled<MoveOverride>().WithEntityAccess())
        {
            Entity targetEntity = target.ValueRO.targetEntity;

            if (targetEntity == Entity.Null || !SystemAPI.Exists(targetEntity))
            {
                target.ValueRW.targetEntity = Entity.Null;
                continue;
            }

            if (!SystemAPI.HasComponent<Health>(targetEntity))
            {
                target.ValueRW.targetEntity = Entity.Null;
                continue;
            }

            LocalTransform targetLocalTransform = SystemAPI.GetComponent<LocalTransform>(targetEntity);

            if (math.distance(localTransform.ValueRO.Position, targetLocalTransform.Position) > shootAttack.ValueRO.attackDistance)
            {
                MovementAgentAPI.SetTarget(state.EntityManager, entity, targetLocalTransform.Position, gridComponent, ecb);
                continue; // Chưa vào tầm -> Di chuyển tiếp và bỏ qua bắn
            }
            else
            {
                MovementAgentAPI.StopAgent(state.EntityManager, entity, ecb);
            }

            var steering = SystemAPI.GetComponent<MovementSteeringComponent>(entity);
            float3 aimDirection = math.normalize(targetLocalTransform.Position - localTransform.ValueRO.Position);

            if (!aimDirection.Equals(float3.zero))
            {
                quaternion targetRotation = quaternion.LookRotationSafe(aimDirection, math.up());
                localTransform.ValueRW.Rotation = math.slerp(localTransform.ValueRO.Rotation, targetRotation, steering.rotationSpeed * SystemAPI.Time.DeltaTime);
            }

            var buffer = weaponBuffer;

            for (int i = 0; i < buffer.Length; i++)
            {
                ref UnitWeaponSlot currentWeapon = ref buffer.ElementAt(i);

                currentWeapon.timer -= SystemAPI.Time.DeltaTime;

                if (currentWeapon.timer > 0f)
                    continue; 

                currentWeapon.timer = currentWeapon.timerMax;

                if (currentWeapon.bulletPrefab == Entity.Null)
                    continue;

                SpawnBullet(ref state, localTransform.ValueRO, currentWeapon, targetEntity, unit.ValueRO.playerID);
            }
        }
    }

    private void SpawnBullet(ref SystemState state, LocalTransform unitTransform, UnitWeaponSlot weapon, Entity targetEntity, int shooterPlayerID)
    {
        Entity bulletEntity = state.EntityManager.Instantiate(weapon.bulletPrefab);

        float3 bulletSpawnWorldPosition = unitTransform.TransformPoint(weapon.bulletSpawnLocalPos);

        var bulletTransform = SystemAPI.GetComponent<LocalTransform>(bulletEntity);
        SystemAPI.SetComponent(bulletEntity, bulletTransform.WithPosition(bulletSpawnWorldPosition));

        if (SystemAPI.HasComponent<Bullet>(bulletEntity))
        {
            RefRW<Bullet> bullet = SystemAPI.GetComponentRW<Bullet>(bulletEntity);
            bullet.ValueRW.damage = weapon.damage;

        }
        else if (SystemAPI.HasComponent<ArtilleryBullet>(bulletEntity))
        {
            RefRW<ArtilleryBullet> artBullet = SystemAPI.GetComponentRW<ArtilleryBullet>(bulletEntity);
            artBullet.ValueRW.aoeDamage = weapon.damage;
            artBullet.ValueRW.speed = weapon.bulletSpeed;
        }

        if (SystemAPI.HasComponent<Unit>(bulletEntity))
        {
            SystemAPI.SetComponent(bulletEntity, new Unit { playerID = shooterPlayerID });
        }

        RefRW<Target> bulletTarget = SystemAPI.GetComponentRW<Target>(bulletEntity);
        bulletTarget.ValueRW.targetEntity = targetEntity;
    }
}