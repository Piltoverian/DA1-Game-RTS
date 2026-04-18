using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

partial struct BulletMoverSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        var healthLookup = SystemAPI.GetComponentLookup<Health>(false);
        var shootVictimLookup = SystemAPI.GetComponentLookup<ShootVictim>(true);

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
                // Lấy transform của unit và cộng thêm offset đã chuyển sang World Space
                var victimTransform = transformLookup[targetEntity];
                targetPosition = victimTransform.TransformPoint(shootVictimLookup[targetEntity].localHitOffset);
            }
            else
            {
                targetPosition = transformLookup[targetEntity].Position;
            }



            float3 currentPosition = localTransform.ValueRO.Position;

            float distanceBeforeSq = math.distancesq(currentPosition, targetPosition);
            
            float3 moveDirection = math.normalize(targetPosition - currentPosition);
            float deltaTime = SystemAPI.Time.DeltaTime;

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
                }

                ecb.DestroyEntity(entity);
            }

        }
    }

}
