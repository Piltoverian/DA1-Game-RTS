using System.Diagnostics;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using static ProductionAuthoring;

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

        foreach (var (prod, buildingTransform,entity) in
                 SystemAPI.Query<RefRW<ProductionData>, RefRO<LocalTransform>>()
                     .WithNone<UnderConstructionTag>().WithEntityAccess())
        {
            var queuebuffer= state.EntityManager.GetBuffer<ProductionQueueElement>(entity);
            if (queuebuffer.IsEmpty)
                continue;

            if (prod.ValueRO.TimeRemaining <= 0f)
            {
                prod.ValueRW.TimeRemaining = prod.ValueRO.ProductionTime;
            }

            prod.ValueRW.TimeRemaining -= dt;

            if (prod.ValueRO.TimeRemaining > 0f)
                continue;

            if (queuebuffer[0].UnitPrefab == Entity.Null)
            {
                UnityEngine.Debug.LogError("UnitPrefab is NULL");
                continue;
            }

            if (!state.EntityManager.HasComponent<Unit>(entity))
            {
                Debug.WriteLine("Building need to be a unit for know who is it owner");
            }
            var untiComponent = state.EntityManager.GetComponentData<Unit>(entity);
            PlayerContext playerContextEntity = new PlayerContext();
            PlayerContextHelper.GetContextData(state.EntityManager, untiComponent.playerID, out playerContextEntity);

            if(playerContextEntity.currentPopulation >= playerContextEntity.maxPopulation)
            {
                UnityEngine.Debug.Log("Max population reached");
                continue;
            }
            Entity unit = ecb.Instantiate(queuebuffer[0].UnitPrefab);
            PlayerContextHelper.UpdatePlayerContext(state.EntityManager, playerContextEntity.PlayerId, PlayerContextDataType.currentPopulation, playerContextEntity.currentPopulation + 1);
              
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

            queuebuffer.RemoveAt(0);

            prod.ValueRW.TimeRemaining =
               queuebuffer.IsEmpty
                    ? prod.ValueRO.ProductionTime
                    : 0f;
        }
    }
}
