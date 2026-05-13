using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial class ProductionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;

        EntityCommandBuffer ecb =
            new EntityCommandBuffer(Allocator.Temp);

        foreach (var (prod, transform, entity) in
                 SystemAPI.Query<
                         RefRW<ProductionData>,
                         RefRO<LocalTransform>>()
                     .WithEntityAccess())
        {
            if (prod.ValueRO.QueueCount <= 0)
                continue;

            prod.ValueRW.TimeRemaining -= dt;

            if (prod.ValueRO.TimeRemaining > 0f)
                continue;

            Entity unit =
                ecb.Instantiate(prod.ValueRO.UnitPrefab);

            float3 spawnPos =
                transform.ValueRO.Position +
                prod.ValueRO.SpawnOffset;

            ecb.SetComponent(
                unit,
                LocalTransform.FromPosition(spawnPos)
            );

            prod.ValueRW.QueueCount--;

            if (prod.ValueRO.QueueCount > 0)
            {
                prod.ValueRW.TimeRemaining =
                    prod.ValueRO.ProductionTime;
            }
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}