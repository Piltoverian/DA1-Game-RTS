using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
partial struct ArtilleryBulletSystem : ISystem
{
    private ComponentLookup<LocalTransform> transformLookup;
    private ComponentLookup<EntityHealth> healthLookup;
    private ComponentLookup<EntityOwner> unitLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridComponent>();
        state.RequireForUpdate<UnitSpatialBucket>();
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

        transformLookup = state.GetComponentLookup<LocalTransform>(true);
        healthLookup = state.GetComponentLookup<EntityHealth>(false);
        unitLookup = state.GetComponentLookup<EntityOwner>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        transformLookup.Update(ref state);
        healthLookup.Update(ref state);
        unitLookup.Update(ref state);

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        float deltaTime = SystemAPI.Time.DeltaTime;
        using var contexts = SystemAPI.QueryBuilder().WithAll<PlayerContext>().Build()
            .ToComponentDataArray<PlayerContext>(Allocator.Temp);

        GridComponent gridComponent = SystemAPI.GetSingleton<GridComponent>();
        UnitSpatialBucket spatialBucket = SystemAPI.GetSingleton<UnitSpatialBucket>();
        var bucketMap = spatialBucket.Bucket;

        int2 gridMax = new int2(gridComponent.width - 1, gridComponent.height - 1);

        foreach (var (localTransform, bullet, target, unit, entity) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRW<ArtilleryBullet>, RefRO<Target>, RefRO<EntityOwner>>().WithEntityAccess())
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
                int shooterPID = bullet.ValueRO.playerID != -1 ? bullet.ValueRO.playerID : unit.ValueRO.PlayerID;

                int2 centerCell = GridHelper.WorldToGrid(explosionPos, gridComponent);

                int radiusInCells = (int)math.ceil(aoeRadius / gridComponent.cellsize);
                int2 aoeMin = math.clamp(centerCell - radiusInCells, 0, gridMax);
                int2 aoeMax = math.clamp(centerCell + radiusInCells, 0, gridMax);
                // A building may be indexed in several footprint cells inside this explosion.
                using var visited = new NativeHashSet<Entity>(16, Allocator.Temp);

                for (int y = aoeMin.y; y <= aoeMax.y; y++)
                {
                    for (int x = aoeMin.x; x <= aoeMax.x; x++)
                    {
                        int cellIndex = GridHelper.GetNodeIndex(new int2(x, y), gridComponent);
                        if (bucketMap.TryGetFirstValue(cellIndex, out Entity victim, out var it))
                        {
                            do
                            {
                                if (!visited.Add(victim)) continue;
                                if (unitLookup.HasComponent(victim) &&
                                    !PlayerContextHelper.AreAllies(contexts, unitLookup[victim].PlayerID, shooterPID))
                                {
                                    if (healthLookup.HasComponent(victim) && transformLookup.HasComponent(victim))
                                    {
                                        float distSq = math.distancesq(transformLookup[victim].Position.xz, explosionPos.xz);
                                        if (distSq <= aoeRadiusSq)
                                        {
                                            var hp = healthLookup.GetRefRW(victim);
                                            if (hp.ValueRO.CurrentHP > 0f)
                                            {
                                                hp.ValueRW.CurrentHP -= aoeDamage;
                                                hp.ValueRW.Changed = true;
                                            }
                                        }
                                    }
                                }
                            } while (bucketMap.TryGetNextValue(out victim, ref it));
                        }
                    }
                }

            }
        }
    }
}
