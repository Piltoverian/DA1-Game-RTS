using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
partial struct ArtilleryBulletSystem : ISystem
{
    private ComponentLookup<LocalTransform> transformLookup;
    private ComponentLookup<Health> healthLookup;
    private ComponentLookup<Unit> unitLookup;
    private ComponentLookup<Target> targetLookup;
    private ComponentLookup<MoveOverride> moveOverrideLookup;
    private ComponentLookup<ShootAttack> shootAttackLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridComponent>();
        state.RequireForUpdate<UnitSpatialBucket>();
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

        transformLookup = state.GetComponentLookup<LocalTransform>(true);
        healthLookup = state.GetComponentLookup<Health>(false);
        unitLookup = state.GetComponentLookup<Unit>(true);
        targetLookup = state.GetComponentLookup<Target>(false);
        moveOverrideLookup = state.GetComponentLookup<MoveOverride>(true);
        shootAttackLookup = state.GetComponentLookup<ShootAttack>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        transformLookup.Update(ref state);
        healthLookup.Update(ref state);
        unitLookup.Update(ref state);
        targetLookup.Update(ref state);
        moveOverrideLookup.Update(ref state);
        shootAttackLookup.Update(ref state);

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        float deltaTime = SystemAPI.Time.DeltaTime;

        GridComponent gridComponent = SystemAPI.GetSingleton<GridComponent>();
        UnitSpatialBucket spatialBucket = SystemAPI.GetSingleton<UnitSpatialBucket>();
        var bucketMap = spatialBucket.Bucket;

        float maxChaseDistSq = 25f * 25f;
        int2 gridMax = new int2(gridComponent.width - 1, gridComponent.height - 1);

        foreach (var (localTransform, bullet, target, unit, entity) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRW<ArtilleryBullet>, RefRO<Target>, RefRO<Unit>>().WithEntityAccess())
        {
            if (bullet.ValueRO.distance == 0)
            {
                Entity targetEntity = target.ValueRO.targetEntity;
                if (targetEntity == Entity.Null || !transformLookup.HasComponent(targetEntity))
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                bullet.ValueRW.startPosition = localTransform.ValueRO.Position;
                bullet.ValueRW.targetPosition = transformLookup[targetEntity].Position;
                bullet.ValueRW.distance = math.distance(bullet.ValueRW.startPosition, bullet.ValueRW.targetPosition);

                if (bullet.ValueRW.distance <= 0.0000001f) bullet.ValueRW.distance = 0.0000001f;
            }

            bullet.ValueRW.distanceTraveled += bullet.ValueRO.speed * deltaTime;
            float t = math.saturate(bullet.ValueRO.distanceTraveled / bullet.ValueRO.distance);

            float3 lerpPos = math.lerp(bullet.ValueRO.startPosition, bullet.ValueRO.targetPosition, t);
            float heightOffset = 4f * bullet.ValueRO.maxHeight * t * (1f - t);
            lerpPos.y += heightOffset;
            localTransform.ValueRW.Position = lerpPos;

            if (t >= 1f)
            {
                ecb.DestroyEntity(entity);

                float3 explosionPos = bullet.ValueRO.targetPosition;
                float aoeRadius = bullet.ValueRO.aoeRadius;
                float aoeRadiusSq = aoeRadius * aoeRadius;
                float aoeDamage = bullet.ValueRO.aoeDamage;
                int shooterPID = bullet.ValueRO.playerID != -1 ? bullet.ValueRO.playerID : unit.ValueRO.playerID;
                Entity attacker = bullet.ValueRO.sourceEntity;

                int2 centerCell = GridHelper.WorldToGrid(explosionPos, gridComponent);

                int radiusInCells = (int)math.ceil(aoeRadius / gridComponent.cellsize);
                int2 aoeMin = math.clamp(centerCell - radiusInCells, 0, gridMax);
                int2 aoeMax = math.clamp(centerCell + radiusInCells, 0, gridMax);

                for (int y = aoeMin.y; y <= aoeMax.y; y++)
                {
                    for (int x = aoeMin.x; x <= aoeMax.x; x++)
                    {
                        int cellIndex = GridHelper.GetNodeIndex(new int2(x, y), gridComponent);
                        if (bucketMap.TryGetFirstValue(cellIndex, out Entity victim, out var it))
                        {
                            do
                            {
                                if (unitLookup.HasComponent(victim) && unitLookup[victim].playerID != shooterPID)
                                {
                                    if (healthLookup.HasComponent(victim) && transformLookup.HasComponent(victim))
                                    {
                                        float distSq = math.distancesq(transformLookup[victim].Position, explosionPos);
                                        if (distSq <= aoeRadiusSq)
                                        {
                                            var hp = healthLookup.GetRefRW(victim);
                                            if (hp.ValueRO.healthAmount > 0f)
                                            {
                                                hp.ValueRW.healthAmount -= aoeDamage;
                                                hp.ValueRW.OnHealthChanged = true;
                                            }
                                        }
                                    }
                                }
                            } while (bucketMap.TryGetNextValue(out victim, ref it));
                        }
                    }
                }

                bool isAttackerAlive = attacker != Entity.Null &&
                                       healthLookup.HasComponent(attacker) &&
                                       healthLookup[attacker].healthAmount > 0f;

                if (isAttackerAlive)
                {
                    int2 distressMin = math.clamp(centerCell - 5, 0, gridMax);
                    int2 distressMax = math.clamp(centerCell + 5, 0, gridMax);

                    for (int y = distressMin.y; y <= distressMax.y; y++)
                    {
                        for (int x = distressMin.x; x <= distressMax.x; x++)
                        {
                            int cellIndex = GridHelper.GetNodeIndex(new int2(x, y), gridComponent);
                            if (bucketMap.TryGetFirstValue(cellIndex, out Entity ally, out var it))
                            {
                                do
                                {
                                    if (unitLookup.HasComponent(ally) && unitLookup[ally].playerID != shooterPID)
                                    {
                                        bool allyIdle = targetLookup.HasComponent(ally) &&
                                                        targetLookup[ally].targetEntity == Entity.Null &&
                                                        (!moveOverrideLookup.HasComponent(ally) || !moveOverrideLookup.IsComponentEnabled(ally));

                                        if (allyIdle && shootAttackLookup.HasComponent(ally))
                                        {
                                            float distToAttackerSq = transformLookup.HasComponent(attacker)
                                                ? math.distancesq(transformLookup[ally].Position, transformLookup[attacker].Position)
                                                : 0f;

                                            if (distToAttackerSq <= maxChaseDistSq)
                                            {
                                                var allyTarget = targetLookup.GetRefRW(ally);
                                                allyTarget.ValueRW.targetEntity = attacker;
                                            }
                                        }
                                    }
                                } while (bucketMap.TryGetNextValue(out ally, ref it));
                            }
                        }
                    }
                }
            }
        }
    }
}