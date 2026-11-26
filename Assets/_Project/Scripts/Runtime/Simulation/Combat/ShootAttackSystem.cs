using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial struct ShootAttackSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridComponent>();
        state.RequireForUpdate<GameDataRegistryComponent>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!ServerMatch.isGamePlayCanRun(ref state)) return;
        float deltaTime = SystemAPI.Time.DeltaTime;

        GridComponent gridComponent =
            SystemAPI.GetSingleton<GridComponent>();

        // Playback this update, before late death cleanup. A next-frame buffer can
        // otherwise write movement/weapon data to a shooter destroyed this frame.
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        ComponentLookup<MoveOverride> moveOverrideLookup =
            SystemAPI.GetComponentLookup<MoveOverride>(true);

        ComponentLookup<MovementSteeringComponent> steeringLookup =
            SystemAPI.GetComponentLookup<MovementSteeringComponent>(true);
        var transforms = SystemAPI.GetComponentLookup<LocalTransform>(true);
        var footprints = SystemAPI.GetComponentLookup<BlockageData>(true);
        bool hasFog = SystemAPI.TryGetSingleton<FogOfWarSpatialSystemData>(out var fog);
        var registryEntity = SystemAPI.GetSingletonEntity<GameDataRegistryComponent>();
        var registry = SystemAPI.GetBuffer<RegistryBlobElement>(registryEntity);
        var Prefab= SystemAPI.GetBuffer<RegistryPrefabElement>(registryEntity);
        using var contexts = SystemAPI.QueryBuilder().WithAll<PlayerContext>().Build()
            .ToComponentDataArray<PlayerContext>(Allocator.Temp);
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
            if (SystemAPI.HasComponent<EntityHealth>(entity) && SystemAPI.GetComponent<EntityHealth>(entity).CurrentHP <= 0) continue;
            for (int slot = 0; slot < weaponBuffer.Length; slot++)
                weaponBuffer.ElementAt(slot).timer = math.max(0, weaponBuffer[slot].timer - deltaTime);
            if (SystemAPI.HasComponent<BuildingConstruction>(entity) && SystemAPI.GetComponent<BuildingConstruction>(entity).Phase != ConstructionPhase.Completed) continue;
            Entity targetEntity = target.ValueRO.targetEntity;
            var attackReference = registry.GetBlobByID<AttackBlob>(shootAttack.ValueRO.IDs, out var result);
            if (result != FunctionResult.Success)
                continue;
            ref var attackBlob = ref attackReference.Value;
            /*
             * Nếu MoveOverride đang enabled nghĩa là người chơi đang ra lệnh move / hit-and-run.
             * ShootAttack không được StopAgent, không được chase, không được disable MoveOverride.
             */
            if (IsMoveOverrideActive(entity, ref moveOverrideLookup))
            {
                target.ValueRW = default;
                continue;
            }

            if (targetEntity == Entity.Null) continue;
            if (!FogOrderRules.IsVisible(unit.ValueRO.PlayerID, targetEntity, hasFog, gridComponent, fog, transforms, footprints))
            {
                if (target.ValueRO.lastSeenEntity != targetEntity
                    || !SystemAPI.HasComponent<MovementAgentComponent>(entity))
                {
                    ForgetTarget(state.EntityManager, entity, ref target.ValueRW, ecb);
                    continue;
                }
                var agent = SystemAPI.GetComponent<MovementAgentComponent>(entity);
                var lastSeen = target.ValueRO.lastSeenPosition;
                float stoppingDistance = steeringLookup.HasComponent(entity)
                    ? math.max(.1f, steeringLookup[entity].stoppingDistance) : .1f;
                bool sameGoal = math.distancesq(agent.currentworldtarget, lastSeen) < .0001f;
                bool finished = sameGoal && !agent.hastarget
                    && (agent.stopReason == MovementStopReason.Arrived
                        || agent.stopReason == MovementStopReason.CrowdedDestination
                        || agent.stopReason == MovementStopReason.InvalidTarget);
                if (math.distancesq(localTransform.ValueRO.Position.xz, lastSeen.xz) <= stoppingDistance * stoppingDistance || finished)
                    ForgetTarget(state.EntityManager, entity, ref target.ValueRW, ecb);
                else if (!sameGoal || !agent.hastarget)
                    MovementAgentAPI.SetTarget(state.EntityManager, entity, lastSeen, gridComponent, ecb);
                continue;
            }
            if (!IsValidTarget(ref state, targetEntity, unit.ValueRO.PlayerID, contexts))
            {
                ForgetTarget(state.EntityManager, entity, ref target.ValueRW, ecb);
                continue;
            }

            LocalTransform targetTransform =
                SystemAPI.GetComponent<LocalTransform>(targetEntity);
            target.ValueRW.lastSeenEntity = targetEntity;
            target.ValueRW.lastSeenPosition = targetTransform.Position;

            float3 unitPosition = localTransform.ValueRO.Position;
            float3 targetPosition = targetTransform.Position;

            float3 toTarget = targetPosition - unitPosition;

            // Chỉ xét mặt phẳng XZ để tank không bị chúi đầu/lật khi target cao/thấp hơn.
            toTarget.y = 0f;

            float distanceSq = math.lengthsq(toTarget);
            if (SystemAPI.HasComponent<BuildingComponent>(targetEntity) && SystemAPI.HasComponent<BlockageData>(targetEntity))
            {
                // A large building is reached at its footprint, not its blocked center.
                var rect = SystemAPI.GetComponent<BlockageData>(targetEntity).LocalRect;
                float2 closest = math.clamp(unitPosition.xz, targetPosition.xz + rect.MinPoint, targetPosition.xz + rect.MaxPoint);
                distanceSq = math.distancesq(unitPosition.xz, closest);
            }

            float attackDistanceSq =attackBlob.Range * attackBlob.Range;
            if (distanceSq > attackDistanceSq)
            {
                if (SystemAPI.HasComponent<MovementAgentComponent>(entity))
                {
                    var movement = SystemAPI.GetComponent<MovementAgentComponent>(entity);
                    float refreshDistance = math.max(1f, gridComponent.cellsize);
                    // Let a requested field finish. Reissuing every frame invalidates
                    // navigation and prevents stable pursuit of a moving opponent.
                    if (!movement.hastarget || math.distancesq(movement.currentworldtarget.xz, targetPosition.xz) > refreshDistance * refreshDistance)
                        MovementAgentAPI.SetTarget(state.EntityManager, entity, targetPosition, gridComponent, ecb);
                }
                else
                {
                    // Stationary weapons cannot chase a target outside firing range.
                    target.ValueRW = default;
                }

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

            var facing = localTransform.ValueRO;
            if (SystemAPI.HasComponent<StationaryDefenseAim>(entity))
            {
                // Capture the grid-fitted model only after runtime snapping has finished.
                if (SystemAPI.HasComponent<Snapping>(entity) && SystemAPI.IsComponentEnabled<Snapping>(entity)) continue;
                var aim = SystemAPI.GetComponent<StationaryDefenseAim>(entity);
                if (!aim.Initialized)
                {
                    aim.RestModel = SystemAPI.HasComponent<PostTransformMatrix>(entity)
                        ? SystemAPI.GetComponent<PostTransformMatrix>(entity).Value : float4x4.identity;
                    aim.Rotation = quaternion.identity; aim.Initialized = true;
                }
                facing.Rotation = aim.Rotation;
                RotateYawOnlyTowardTarget(ref facing, toTarget, deltaTime, attackBlob.RotationSpeed, ref steeringLookup);
                aim.Rotation = facing.Rotation;
                SystemAPI.SetComponent(entity, aim);
                var model = new PostTransformMatrix { Value = math.mul(float4x4.TRS(float3.zero, aim.Rotation, new float3(1)), aim.RestModel) };
                if (SystemAPI.HasComponent<PostTransformMatrix>(entity)) SystemAPI.SetComponent(entity, model);
                else ecb.AddComponent(entity, model);
            }
            else
            {
                RotateYawOnlyTowardTarget(ref localTransform.ValueRW, toTarget, deltaTime, attackBlob.RotationSpeed, ref steeringLookup);
                facing = localTransform.ValueRO;
            }

            if (!IsFacingTargetYawOnly(facing, toTarget))
                continue;

            for (int i = 0; i < weaponBuffer.Length && i < attackBlob.Weapons.Length; i++)
            {
                ref UnitWeaponSlot weapon =
                    ref weaponBuffer.ElementAt(i);
                ref WeaponDefinitionBlob weaponDefinition =
                    ref attackBlob.Weapons[i];
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
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    private static void ForgetTarget(EntityManager em, Entity entity, ref Target target, EntityCommandBuffer ecb)
    {
        target = default;
        MovementAgentAPI.ClearTarget(em, entity, ecb);
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
        int attackerPlayerID,
        NativeArray<PlayerContext> contexts)
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

            if (PlayerContextHelper.AreAllies(contexts, targetUnit.PlayerID, attackerPlayerID))
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
        float3 localSpawn = weapon.SpawnOffset;
        // Grid-fitted buildings keep model scale/centering in PostTransformMatrix.
        // Their muzzle must follow the same matrix as the visible model.
        if (state.EntityManager.HasComponent<BuildingComponent>(shooterEntity) &&
            state.EntityManager.HasComponent<PostTransformMatrix>(shooterEntity))
            localSpawn = math.transform(state.EntityManager.GetComponentData<PostTransformMatrix>(shooterEntity).Value, localSpawn);
        float3 spawnWorldPosition = shooterTransform.TransformPoint(localSpawn);

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

