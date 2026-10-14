using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
partial struct BulletMoverSystem : ISystem
{
    private ComponentLookup<LocalTransform> transformLookup;
    private ComponentLookup<EntityHealth> healthLookup;
    private ComponentLookup<ShootVictim> shootVictimLookup;
    private ComponentLookup<Target> targetLookup;
    private ComponentLookup<MoveOverride> moveOverrideLookup;
    private ComponentLookup<ShootAttack> shootAttackLookup;
    private ComponentLookup<EntityOwner> unitLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridComponent>();
        state.RequireForUpdate<UnitSpatialBucket>();
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

        transformLookup = state.GetComponentLookup<LocalTransform>(true);
        healthLookup = state.GetComponentLookup<EntityHealth>(false);
        shootVictimLookup = state.GetComponentLookup<ShootVictim>(true);
        targetLookup = state.GetComponentLookup<Target>(false);
        moveOverrideLookup = state.GetComponentLookup<MoveOverride>(true);
        shootAttackLookup = state.GetComponentLookup<ShootAttack>(true);
        unitLookup = state.GetComponentLookup<EntityOwner>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        transformLookup.Update(ref state);
        healthLookup.Update(ref state);
        shootVictimLookup.Update(ref state);
        targetLookup.Update(ref state);
        moveOverrideLookup.Update(ref state);
        shootAttackLookup.Update(ref state);
        unitLookup.Update(ref state);

        EntityCommandBuffer ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        GridComponent gridComponent = SystemAPI.GetSingleton<GridComponent>();
        UnitSpatialBucket spatialBucket = SystemAPI.GetSingleton<UnitSpatialBucket>();
        var bucketMap = spatialBucket.Bucket;

        float deltaTime = SystemAPI.Time.DeltaTime;
        using var contexts = SystemAPI.QueryBuilder().WithAll<PlayerContext>().Build()
            .ToComponentDataArray<PlayerContext>(Allocator.Temp);
        int2 gridMax = new int2(gridComponent.width - 1, gridComponent.height - 1);

        foreach (var (localTransform, bullet, target, entity) in
            SystemAPI.Query<RefRW<LocalTransform>, RefRO<Bullet>, RefRO<Target>>()
            .WithEntityAccess())
        {
            Entity targetEntity = target.ValueRO.targetEntity;
            if (targetEntity == Entity.Null || !transformLookup.HasComponent(targetEntity))
            {
                ecb.DestroyEntity(entity);
                continue;
            }

            float3 targetPosition;
            if (shootVictimLookup.HasComponent(targetEntity))
            {
                var victimTransform = transformLookup[targetEntity];
                targetPosition = victimTransform.TransformPoint(shootVictimLookup[targetEntity].localHitOffset);
            }
            else
            {
                targetPosition = transformLookup[targetEntity].Position;
            }

            float3 currentPosition = localTransform.ValueRO.Position;
            float distanceSq = math.distancesq(currentPosition, targetPosition);
            float stepDistance = math.max(0f, bullet.ValueRO.speed * deltaTime);
            // Reach the target this update when the remaining distance fits in the step.
            bool reachedTarget = distanceSq <= stepDistance * stepDistance;
            if (reachedTarget)
            {
                localTransform.ValueRW.Position = targetPosition;
            }
            else
            {
                float3 moveDirection = math.normalizesafe(targetPosition - currentPosition);
                localTransform.ValueRW.Position += moveDirection * stepDistance;
            }

            if (reachedTarget)
            {
                // Ownership, teams and health may have changed while the bullet was in flight.
                if (!healthLookup.HasComponent(targetEntity) || healthLookup[targetEntity].CurrentHP <= 0f ||
                    (unitLookup.HasComponent(targetEntity) && PlayerContextHelper.AreAllies(
                        contexts, bullet.ValueRO.playerID, unitLookup[targetEntity].PlayerID)))
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }
                if (healthLookup.HasComponent(targetEntity))
                {
                    var health = healthLookup.GetRefRW(targetEntity);
                    health.ValueRW.CurrentHP -= bullet.ValueRO.damage;
                    health.ValueRW.Changed = true;
                }

                Entity attacker = bullet.ValueRO.sourceEntity;
                int attackerFaction = bullet.ValueRO.playerID;

                bool isAttackerAlive = attacker != Entity.Null &&
                                       healthLookup.HasComponent(attacker) &&
                                       healthLookup[attacker].CurrentHP > 0f;

                if (isAttackerAlive && targetLookup.HasComponent(targetEntity))
                {
                    var victimTarget = targetLookup.GetRefRW(targetEntity);
                    bool isMoving = moveOverrideLookup.HasComponent(targetEntity) &&
                                    moveOverrideLookup.IsComponentEnabled(targetEntity);

                    if (victimTarget.ValueRO.targetEntity == Entity.Null && !isMoving)
                    {
                        victimTarget.ValueRW.targetEntity = attacker;

                        int victimFaction = unitLookup.HasComponent(targetEntity) ? unitLookup[targetEntity].PlayerID : -1;
                        float3 victimPos = transformLookup[targetEntity].Position;
                        int2 centerCell = GridHelper.WorldToGrid(victimPos, gridComponent);

                        int2 distressMin = math.clamp(centerCell - 5, 0, gridMax);
                        int2 distressMax = math.clamp(centerCell + 5, 0, gridMax);

                        for (int dy = distressMin.y; dy <= distressMax.y; dy++)
                        {
                            for (int dx = distressMin.x; dx <= distressMax.x; dx++)
                            {
                                int cellIndex = GridHelper.GetNodeIndex(new int2(dx, dy), gridComponent);

                                if (bucketMap.TryGetFirstValue(cellIndex, out Entity ally, out var it))
                                {
                                    do
                                    {
                                        if (ally == targetEntity) continue;

                                        if (victimFaction != -1 && unitLookup.HasComponent(ally) &&
                                            PlayerContextHelper.AreAllies(contexts, unitLookup[ally].PlayerID, victimFaction) &&
                                            !PlayerContextHelper.AreAllies(contexts, unitLookup[ally].PlayerID, attackerFaction))
                                        {
                                            bool allyIdle = targetLookup.HasComponent(ally) &&
                                                            targetLookup[ally].targetEntity == Entity.Null &&
                                                            (!moveOverrideLookup.HasComponent(ally) || !moveOverrideLookup.IsComponentEnabled(ally));

                                            if (allyIdle && shootAttackLookup.HasComponent(ally))
                                            {
                                                var allyTarget = targetLookup.GetRefRW(ally);
                                                allyTarget.ValueRW.targetEntity = attacker;
                                            }
                                        }
                                    } while (bucketMap.TryGetNextValue(out ally, ref it));
                                }
                            }
                        }
                    }
                }

                ecb.DestroyEntity(entity);
            }
        }
    }
}
