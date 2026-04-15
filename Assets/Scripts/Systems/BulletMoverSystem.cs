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
            float3 targetPosition = transformLookup[targetEntity].Position;
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
