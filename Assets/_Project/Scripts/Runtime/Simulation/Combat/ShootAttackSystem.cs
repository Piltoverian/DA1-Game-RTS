using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateBefore(typeof(HealthDeadTestSystem))]
public partial struct ShootAttackSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridComponent>();
        state.RequireForUpdate<GameDataRegistryComponent>();
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
        var registryEntity = SystemAPI.GetSingletonEntity<GameDataRegistryComponent>();
        var registry = SystemAPI.GetBuffer<RegistryBlobElement>(registryEntity);
        var Prefab= SystemAPI.GetBuffer<RegistryPrefabElement>(registryEntity);
        foreach (var (
                     localTransform,
                     shootAttack,
                     target,
                     weaponBuffer,
                     unit,
                     entity)
                 in SystemAPI.Query<
                         RefRW<LocalTransform>,
                         RefRO<ShootAttack>,
                         RefRW<Target>,
                         DynamicBuffer<UnitWeaponSlot>,
                         RefRO<EntityOwner>>()
                     .WithEntityAccess())
        {
            if (SystemAPI.HasComponent<BuildingConstruction>(entity) && SystemAPI.GetComponent<BuildingConstruction>(entity).Phase != ConstructionPhase.Completed) continue;
            Entity targetEntity = target.ValueRO.targetEntity;
            var attackReference = registry.GetBlobByID<AttackBlob>(shootAttack.ValueRO.IDs, out var result);
            if (result != FunctionResult.Success)
                continue;
            ref var attackBlob = ref attackReference.Value;
            if (!IsValidTarget(ref state, targetEntity, unit.ValueRO.PlayerID))
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

            float attackDistanceSq =attackBlob.Range * attackBlob.Range;
            if (distanceSq > attackDistanceSq)
            {
                if (SystemAPI.HasComponent<MovementAgentComponent>(entity)) MovementAgentAPI.SetTarget(
                    state.EntityManager,
                    entity,
                    targetPosition,
                    gridComponent,
                    ecb
                );

                continue;
            }

            // Trong tầm bắn thì dừng lại để bắn.
            // Chỉ gọi PauseAgent nếu agent đang có target để tránh record ECB thừa.
            if (SystemAPI.HasComponent<MovementAgentComponent>(entity) && SystemAPI.GetComponent<MovementAgentComponent>(entity).hastarget)
            {
                MovementAgentAPI.PauseAgent(
                    state.EntityManager,
                    entity,
                    ecb
                );
            }

            RotateYawOnlyTowardTarget(
                ref localTransform.ValueRW,
                toTarget,
                deltaTime,
                attackBlob.RotationSpeed,
                ref steeringLookup
            );

            if (!IsFacingTargetYawOnly(localTransform.ValueRO, toTarget))
                continue;

            for (int i = 0; i < weaponBuffer.Length && i < attackBlob.Weapons.Length; i++)
            {
                ref UnitWeaponSlot weapon =
                    ref weaponBuffer.ElementAt(i);
                ref WeaponDefinitionBlob weaponDefinition =
                    ref attackBlob.Weapons[i];
                weapon.timer -= deltaTime;

                if (weapon.timer > 0f)
                    continue;

                weapon.timer = weaponDefinition.Cooldown;
                if (weaponDefinition.ProjectilePrefabIndex < 0 || weaponDefinition.ProjectilePrefabIndex >= Prefab.Length) continue;
                Entity BulletPreFab = Prefab[weaponDefinition.ProjectilePrefabIndex].Prefab;
                if (BulletPreFab == Entity.Null)
                    continue;

                SpawnProjectile(
                    ref state,
                    ecb,
                    localTransform.ValueRO,
                    weaponDefinition,
                    BulletPreFab,
                    targetEntity,
                    unit.ValueRO.PlayerID,
                    entity
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

        if (!SystemAPI.HasComponent<EntityHealth>(targetEntity))
            return false;

        EntityHealth targetHealth = SystemAPI.GetComponent<EntityHealth>(targetEntity);
        if (targetHealth.CurrentHP <= 0f)
            return false;

        if (SystemAPI.HasComponent<EntityOwner>(targetEntity))
        {
            EntityOwner targetUnit =
                SystemAPI.GetComponent<EntityOwner>(targetEntity);

            if (targetUnit.PlayerID == attackerPlayerID)
                return false;
        }

        return true;
    }

    private void RotateYawOnlyTowardTarget(
        ref LocalTransform transform,
        float3 toTarget,
        float deltaTime,
        float rotationSpeed,
        ref ComponentLookup<MovementSteeringComponent> steeringLookup)
    {
        toTarget.y = 0f;

        if (math.lengthsq(toTarget) < 0.001f)
            return;

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
        WeaponDefinitionBlob weapon,
        Entity BulletPrefab,
        Entity targetEntity,
        int shooterPlayerID,
        Entity shooterEntity)
    {
        Entity projectilePrefab = BulletPrefab;

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
            shooterTransform.TransformPoint(weapon.SpawnOffset);

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
            weapon,
            shooterEntity,
            shooterPlayerID
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
        if (!state.EntityManager.HasComponent<EntityOwner>(projectilePrefab))
        {
            ecb.AddComponent(projectileEntity, new EntityOwner { PlayerID = shooterPlayerID });
            return;
        }

        EntityOwner projectileUnit =
            state.EntityManager.GetComponentData<EntityOwner>(projectilePrefab);

        projectileUnit.PlayerID = shooterPlayerID;

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
        WeaponDefinitionBlob weapon,
        Entity shooterEntity,
        int shooterPlayerID)
    {
        /*
         * Loại đạn 1: Bullet thường
         */
        if (state.EntityManager.HasComponent<Bullet>(projectilePrefab))
        {
            Bullet bullet =
                state.EntityManager.GetComponentData<Bullet>(projectilePrefab);

            bullet.damage = weapon.Damage;
            bullet.speed = weapon.ProjectileSpeed;
            bullet.sourceEntity = shooterEntity;
            bullet.playerID = shooterPlayerID;

            ecb.SetComponent(projectileEntity, bullet);
        }

        /*
         * Loại đạn 2: ArtilleryBullet / AOE
         */
        if (state.EntityManager.HasComponent<ArtilleryBullet>(projectilePrefab))
        {
            ArtilleryBullet artilleryBullet =
                state.EntityManager.GetComponentData<ArtilleryBullet>(projectilePrefab);

            artilleryBullet.aoeDamage = weapon.Damage;
            artilleryBullet.speed = weapon.ProjectileSpeed;
            artilleryBullet.sourceEntity = shooterEntity;
            artilleryBullet.playerID = shooterPlayerID;

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

