using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct ProductionSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;

        EntityCommandBuffer ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (prod, buildingTransform) in
                 SystemAPI.Query<RefRW<ProductionData>, RefRO<LocalTransform>>()
                     .WithNone<UnderConstructionTag>())
        {
            if (prod.ValueRO.QueueCount <= 0)
                continue;

            if (prod.ValueRO.TimeRemaining <= 0f)
            {
                prod.ValueRW.TimeRemaining = prod.ValueRO.ProductionTime;
            }

            prod.ValueRW.TimeRemaining -= dt;

            if (prod.ValueRO.TimeRemaining > 0f)
                continue;

            Entity unit = ecb.Instantiate(prod.ValueRO.UnitPrefab);

            float3 buildingPos = buildingTransform.ValueRO.Position;

            float3 spawnPos =
                buildingPos + prod.ValueRO.SpawnOffset;

            float3 rallyPos =
                buildingPos + prod.ValueRO.RallyOffset;

            ecb.SetComponent(
                unit,
                LocalTransform.FromPositionRotationScale(
                    spawnPos,
                    quaternion.identity,
                    1f
                )
            );

            ecb.SetComponent(unit, new MoveOverride
            {
                targetPosition = rallyPos,
                stopDistanceSq = 0.25f,
                targetApplied = false
            });

            ecb.SetComponentEnabled<MoveOverride>(unit, true);

            prod.ValueRW.QueueCount--;

            prod.ValueRW.TimeRemaining =
                prod.ValueRO.QueueCount > 0
                    ? prod.ValueRO.ProductionTime
                    : 0f;
        }
    }
}