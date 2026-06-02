using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateBefore(typeof(HealthDeadTestSystem))]
public partial struct ShootAttackSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridComponent>();
        state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        GridComponent gridComponent =
            SystemAPI.GetSingleton<GridComponent>();

        EntityCommandBuffer ecb =
            SystemAPI
                .GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

        ComponentLookup<MoveOverride> moveOverrideLookup =
            SystemAPI.GetComponentLookup<MoveOverride>(true);

        ComponentLookup<MovementSteeringComponent> steeringLookup =
            SystemAPI.GetComponentLookup<MovementSteeringComponent>(true);

        foreach (var (
                     localTransform,
                     shootAttack,
                     target,
                     movementAgent,
                     weaponBuffer,
                     unit,
                     entity)
                 in SystemAPI.Query<
                         RefRW<LocalTransform>,
                         RefRO<ShootAttack>,
                         RefRW<Target>,
                         RefRO<MovementAgentComponent>,
                         DynamicBuffer<UnitWeaponSlot>,
                         RefRO<Unit>>()
                     .WithEntityAccess())
        {
            Entity targetEntity = target.ValueRO.targetEntity;

            if (!IsValidTarget(ref state, targetEntity, unit.ValueRO.playerID))
            {
                target.ValueRW.targetEntity = Entity.Null;
                continue;
            }

            /*
             * Nếu MoveOverride đang enabled nghĩa là người chơi đang ra lệnh move / hit-and-run.
             * ShootAttack không được StopAgent, không được chase, không được disable MoveOverride.
             */
            if (IsMoveOverrideActive(entity, ref moveOverrideLookup))
            {
                continue;
            }

            LocalTransform targetTransform =
                SystemAPI.GetComponent<LocalTransform>(targetEntity);

            float3 unitPosition = localTransform.ValueRO.Position;
            float3 targetPosition = targetTransform.Position;

            float3 toTarget = targetPosition - unitPosition;

            // Chỉ xét mặt phẳng XZ để tank không bị chúi đầu/lật khi target cao/thấp hơn.
            toTarget.y = 0f;

            float distanceSq = math.lengthsq(toTarget);

            float attackDistanceSq =
                shootAttack.ValueRO.attackDistance *
                shootAttack.ValueRO.attackDistance;

            if (distanceSq > attackDistanceSq)
            {
                MovementAgentAPI.SetTarget(
                    state.EntityManager,
                    entity,
                    targetPosition,
                    gridComponent,
                    ecb
                );

                continue;
            }

            // Trong tầm bắn thì dừng lại để bắn.
            // Chỉ gọi StopAgent nếu agent đang có target để tránh record ECB thừa.
            if (movementAgent.ValueRO.hastarget)
            {
                MovementAgentAPI.StopAgent(
                    state.EntityManager,
                    entity,
                    ecb
                );
            }

            RotateYawOnlyTowardTarget(
                ref localTransform.ValueRW,
                toTarget,
                deltaTime,
                entity,
                ref steeringLookup
            );

            if (!IsFacingTargetYawOnly(localTransform.ValueRO, toTarget))
                continue;

            for (int i = 0; i < weaponBuffer.Length; i++)
            {
                ref UnitWeaponSlot weapon =
                    ref weaponBuffer.ElementAt(i);

                weapon.timer -= deltaTime;

                if (weapon.timer > 0f)
                    continue;

                weapon.timer = weapon.timerMax;

                if (weapon.bulletPrefab == Entity.Null)
                    continue;

                SpawnProjectile(
                    ref state,
                    ecb,
                    localTransform.ValueRO,
                    weapon,
                    targetEntity,
                    unit.ValueRO.playerID
                );
            }
        }
    }

    private bool IsMoveOverrideActive(
        Entity entity,
        ref ComponentLookup<MoveOverride> moveOverrideLookup)
    {
        if (!moveOverrideLookup.HasComponent(entity))
            return false;

        return moveOverrideLookup.IsComponentEnabled(entity);
    }

    private bool IsValidTarget(
        ref SystemState state,
        Entity targetEntity,
        int attackerPlayerID)
    {
        if (targetEntity == Entity.Null)
            return false;

        if (!SystemAPI.Exists(targetEntity))
            return false;

        if (!SystemAPI.HasComponent<LocalTransform>(targetEntity))
            return false;

        if (!SystemAPI.HasComponent<Health>(targetEntity))
            return false;

        if (SystemAPI.HasComponent<Unit>(targetEntity))
        {
            Unit targetUnit =
                SystemAPI.GetComponent<Unit>(targetEntity);

            if (targetUnit.playerID == attackerPlayerID)
                return false;
        }

        if (SystemAPI.HasComponent<BuildingData>(targetEntity))
        {
            BuildingData targetBuilding =
                SystemAPI.GetComponent<BuildingData>(targetEntity);

            if (targetBuilding.PlayerID == attackerPlayerID)
                return false;
        }

        return true;
    }

    private void RotateYawOnlyTowardTarget(
        ref LocalTransform transform,
        float3 toTarget,
        float deltaTime,
        Entity entity,
        ref ComponentLookup<MovementSteeringComponent> steeringLookup)
    {
        toTarget.y = 0f;

        if (math.lengthsq(toTarget) < 0.001f)
            return;

        float rotationSpeed = 10f;

        if (steeringLookup.HasComponent(entity))
        {
            MovementSteeringComponent steering =
                steeringLookup[entity];

            rotationSpeed = steering.rotationSpeed;
        }

        float3 flatDirection =
            math.normalizesafe(toTarget, math.forward());

        quaternion targetRotation =
            quaternion.LookRotationSafe(flatDirection, math.up());

        transform.Rotation = math.slerp(
            transform.Rotation,
            targetRotation,
            math.saturate(rotationSpeed * deltaTime)
        );
    }

    private bool IsFacingTargetYawOnly(
        LocalTransform transform,
        float3 toTarget)
    {
        toTarget.y = 0f;

        if (math.lengthsq(toTarget) < 0.001f)
            return true;

        float3 forward =
            math.mul(transform.Rotation, math.forward());

        forward.y = 0f;

        forward =
            math.normalizesafe(forward, math.forward());

        float3 targetDirection =
            math.normalizesafe(toTarget, math.forward());

        return math.dot(forward, targetDirection) >= 0.95f;
    }

    private void SpawnProjectile(
        ref SystemState state,
        EntityCommandBuffer ecb,
        LocalTransform shooterTransform,
        UnitWeaponSlot weapon,
        Entity targetEntity,
        int shooterPlayerID)
    {
        Entity projectilePrefab = weapon.bulletPrefab;

        Entity projectileEntity =
            ecb.Instantiate(projectilePrefab);

        /*
         * bulletSpawnLocalPos phải được bake từ Muzzle Transform trong ShootAttackAuthoring:
         *
         * muzzleLocalPosition = authoring.transform.InverseTransformPoint(Muzzle.position);
         *
         * Ở đây ta convert local muzzle position sang world position.
         */
        float3 spawnWorldPosition =
            shooterTransform.TransformPoint(weapon.bulletSpawnLocalPos);

        ApplyProjectileTransform(
            ref state,
            ecb,
            projectileEntity,
            projectilePrefab,
            spawnWorldPosition,
            shooterTransform.Rotation
        );

        ApplyProjectileOwner(
            ref state,
            ecb,
            projectileEntity,
            projectilePrefab,
            shooterPlayerID
        );

        ApplyProjectileTarget(
            ref state,
            ecb,
            projectileEntity,
            projectilePrefab,
            targetEntity
        );

        ApplyProjectileDamageData(
            ref state,
            ecb,
            projectileEntity,
            projectilePrefab,
            weapon
        );
    }

    private void ApplyProjectileTransform(
    ref SystemState state,
    EntityCommandBuffer ecb,
    Entity projectileEntity,
    Entity projectilePrefab,
    float3 spawnWorldPosition,
    quaternion shooterRotation)
    {
        if (state.EntityManager.HasComponent<LocalTransform>(projectilePrefab))
        {
            LocalTransform prefabTransform =
                state.EntityManager.GetComponentData<LocalTransform>(projectilePrefab);

            prefabTransform.Position = spawnWorldPosition;
            prefabTransform.Rotation = shooterRotation;
            ecb.SetComponent(projectileEntity, prefabTransform);
        }
        else
        {
            ecb.AddComponent(
                projectileEntity,
                LocalTransform.FromPositionRotationScale(
                    spawnWorldPosition,
                    shooterRotation,
                    1f
                )
            );
        }
    }

    private void ApplyProjectileOwner(
        ref SystemState state,
        EntityCommandBuffer ecb,
        Entity projectileEntity,
        Entity projectilePrefab,
        int shooterPlayerID)
    {
        if (!state.EntityManager.HasComponent<Unit>(projectilePrefab))
            return;

        Unit projectileUnit =
            state.EntityManager.GetComponentData<Unit>(projectilePrefab);

        projectileUnit.playerID = shooterPlayerID;

        ecb.SetComponent(projectileEntity, projectileUnit);
    }

    private void ApplyProjectileTarget(
        ref SystemState state,
        EntityCommandBuffer ecb,
        Entity projectileEntity,
        Entity projectilePrefab,
        Entity targetEntity)
    {
        Target projectileTarget = new Target
        {
            targetEntity = targetEntity
        };

        if (state.EntityManager.HasComponent<Target>(projectilePrefab))
        {
            ecb.SetComponent(projectileEntity, projectileTarget);
        }
        else
        {
            ecb.AddComponent(projectileEntity, projectileTarget);
        }
    }

    private void ApplyProjectileDamageData(
        ref SystemState state,
        EntityCommandBuffer ecb,
        Entity projectileEntity,
        Entity projectilePrefab,
        UnitWeaponSlot weapon)
    {
        /*
         * Loại đạn 1: Bullet thường
         */
        if (state.EntityManager.HasComponent<Bullet>(projectilePrefab))
        {
            Bullet bullet =
                state.EntityManager.GetComponentData<Bullet>(projectilePrefab);

            bullet.damage = weapon.damage;
            bullet.speed = weapon.bulletSpeed;

            ecb.SetComponent(projectileEntity, bullet);
        }

        /*
         * Loại đạn 2: ArtilleryBullet / AOE
         */
        if (state.EntityManager.HasComponent<ArtilleryBullet>(projectilePrefab))
        {
            ArtilleryBullet artilleryBullet =
                state.EntityManager.GetComponentData<ArtilleryBullet>(projectilePrefab);

            artilleryBullet.aoeDamage = weapon.damage;
            artilleryBullet.speed = weapon.bulletSpeed;

            ecb.SetComponent(projectileEntity, artilleryBullet);
        }

        /*
         * Sau này nếu thêm loại đạn khác, thêm nhánh ở đây.
         *
         * Ví dụ:
         *
         * if (state.EntityManager.HasComponent<LaserBullet>(projectilePrefab))
         * {
         *     LaserBullet laser =
         *         state.EntityManager.GetComponentData<LaserBullet>(projectilePrefab);
         *
         *     laser.damage = weapon.damage;
         *     laser.speed = weapon.bulletSpeed;
         *
         *     ecb.SetComponent(projectileEntity, laser);
         * }
         */
    }
}