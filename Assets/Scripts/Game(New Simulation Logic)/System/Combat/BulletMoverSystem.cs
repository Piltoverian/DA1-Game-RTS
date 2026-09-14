using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
partial struct BulletMoverSystem : ISystem
{
    private ComponentLookup<LocalTransform> transformLookup;
    private ComponentLookup<Health> healthLookup;
    private ComponentLookup<ShootVictim> shootVictimLookup;
    private ComponentLookup<Target> targetLookup;
    private ComponentLookup<MoveOverride> moveOverrideLookup;
    private ComponentLookup<ShootAttack> shootAttackLookup;
    private ComponentLookup<Unit> unitLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridComponent>();
        state.RequireForUpdate<UnitSpatialBucket>();
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

        transformLookup = state.GetComponentLookup<LocalTransform>(true);
        healthLookup = state.GetComponentLookup<Health>(false);
        shootVictimLookup = state.GetComponentLookup<ShootVictim>(true);
        targetLookup = state.GetComponentLookup<Target>(false);
        moveOverrideLookup = state.GetComponentLookup<MoveOverride>(true);
        shootAttackLookup = state.GetComponentLookup<ShootAttack>(true);
        unitLookup = state.GetComponentLookup<Unit>(true);
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
            float distanceBeforeSq = math.distancesq(currentPosition, targetPosition);
            
            float3 moveDirection = math.normalizesafe(targetPosition - currentPosition);
            localTransform.ValueRW.Position += moveDirection * bullet.ValueRO.speed * deltaTime;

            float distanceAfterSq = math.distancesq(localTransform.ValueRO.Position, targetPosition);
            if (distanceAfterSq > distanceBeforeSq)
            {
                localTransform.ValueRW.Position = targetPosition;
            }

            float destroyDistanceSq = 0.2f * 0.2f;
            if (math.distancesq(localTransform.ValueRO.Position, targetPosition) <= destroyDistanceSq)
            {
                if (healthLookup.HasComponent(targetEntity))
                {
                    var health = healthLookup.GetRefRW(targetEntity);
                    health.ValueRW.healthAmount -= bullet.ValueRO.damage;
                    health.ValueRW.OnHealthChanged = true;
                }

                Entity attacker = bullet.ValueRO.sourceEntity;
                int attackerFaction = bullet.ValueRO.playerID;

                bool isAttackerAlive = attacker != Entity.Null &&
                                       healthLookup.HasComponent(attacker) &&
                                       healthLookup[attacker].healthAmount > 0f;

                if (isAttackerAlive && targetLookup.HasComponent(targetEntity))
                {
                    var victimTarget = targetLookup.GetRefRW(targetEntity);
                    bool isMoving = moveOverrideLookup.HasComponent(targetEntity) &&
                                    moveOverrideLookup.IsComponentEnabled(targetEntity);

                    if (victimTarget.ValueRO.targetEntity == Entity.Null && !isMoving)
                    {
                        victimTarget.ValueRW.targetEntity = attacker;

                        int victimFaction = unitLookup.HasComponent(targetEntity) ? unitLookup[targetEntity].playerID : -1;
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

                                        if (victimFaction != -1 && unitLookup.HasComponent(ally) && unitLookup[ally].playerID == victimFaction)
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
