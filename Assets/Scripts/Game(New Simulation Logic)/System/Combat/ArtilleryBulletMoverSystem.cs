using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
partial struct ArtilleryBulletSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        float deltaTime = SystemAPI.Time.DeltaTime;

        NativeList<ExplosionEvent> explosions = new NativeList<ExplosionEvent>(Allocator.Temp);

        // 1. THÊM RefRO<Unit> vào Query để lấy playerID của viên đạn
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

                if (bullet.ValueRW.distance <= 0.1f) bullet.ValueRW.distance = 0.1f;
            }

            bullet.ValueRW.distanceTraveled += bullet.ValueRO.speed * deltaTime;
            float t = math.saturate(bullet.ValueRO.distanceTraveled / bullet.ValueRO.distance);

            float3 lerpPos = math.lerp(bullet.ValueRO.startPosition, bullet.ValueRO.targetPosition, t);
            float heightOffset = 4f * bullet.ValueRO.maxHeight * t * (1f - t);
            lerpPos.y += heightOffset;
            localTransform.ValueRW.Position = lerpPos;

            if (t >= 1f)
            {
                explosions.Add(new ExplosionEvent
                {
                    position = bullet.ValueRO.targetPosition,
                    radiusSq = bullet.ValueRO.aoeRadius * bullet.ValueRO.aoeRadius,
                    damage = bullet.ValueRO.aoeDamage,
                    // 2. Lưu playerID của viên đạn vào vụ nổ
                    shooterPlayerID = unit.ValueRO.playerID
                });

                ecb.DestroyEntity(entity);
            }
        }

        if (explosions.Length > 0)
        {
            // 3. THÊM RefRO<Unit> vào Query nạn nhân để lấy playerID của người chịu đòn
            foreach (var (health, healthTransform, victimUnit) in SystemAPI.Query<RefRW<Health>, RefRO<LocalTransform>, RefRO<Unit>>())
            {
                float totalDamageReceived = 0;

                for (int i = 0; i < explosions.Length; i++)
                {
                    // 4. KIỂM TRA ĐỒNG MINH (FRIENDLY FIRE)
                    // Nếu nạn nhân có cùng playerID với viên đạn -> Bỏ qua vụ nổ này
                    if (victimUnit.ValueRO.playerID == explosions[i].shooterPlayerID)
                    {
                        continue;
                    }

                    float distSq = math.distancesq(healthTransform.ValueRO.Position, explosions[i].position);
                    if (distSq <= explosions[i].radiusSq)
                    {
                        totalDamageReceived += explosions[i].damage;
                    }
                }

                if (totalDamageReceived > 0)
                {
                    health.ValueRW.healthAmount -= totalDamageReceived;
                    health.ValueRW.OnHealthChanged = true;
                }
            }
        }

        explosions.Dispose();
    }

    // Struct phụ trợ lưu thông tin vụ nổ
    private struct ExplosionEvent
    {
        public float3 position;
        public float radiusSq;
        public float damage;
        public int shooterPlayerID; // Thêm trường này để đối chiếu phe phái
    }
}