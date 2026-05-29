using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct ProductionSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;

        EntityCommandBuffer ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        BufferLookup<ProductionQueueElement> queueBufferLookup = SystemAPI.GetBufferLookup<ProductionQueueElement>(false);
        ComponentLookup<Unit> unitLookup = SystemAPI.GetComponentLookup<Unit>(true);
        ComponentLookup<MoveOverride> moveOverrideLookup = SystemAPI.GetComponentLookup<MoveOverride>(true);

        foreach (var (prod, buildingTransform, entity) in
                 SystemAPI.Query<RefRW<ProductionData>, RefRO<LocalTransform>>()
                     .WithNone<UnderConstructionTag>()
                     .WithEntityAccess())
        {
            // 1. Kiểm tra xem nhà có Buffer hàng đợi không
            if (!queueBufferLookup.HasBuffer(entity))
            {
                UnityEngine.Debug.Log("ProductionSystem: Building does not have ProductionQueueElement buffer.");
                continue;
            }

            DynamicBuffer<ProductionQueueElement> queueBuffer = queueBufferLookup[entity];

            // 2. Kiểm tra hàng đợi có trống không
            if (queueBuffer.IsEmpty)
            {
                // Dòng này log ra sẽ rất nhiều khi nhà không sản xuất gì, bạn có thể comment lại nếu bị rác Console
                // UnityEngine.Debug.Log("ProductionSystem: Production queue is empty.");
                continue;
            }

            if (prod.ValueRO.TimeRemaining <= 0f)
            {
                prod.ValueRW.TimeRemaining = prod.ValueRO.ProductionTime;
            }

            prod.ValueRW.TimeRemaining -= dt;

            // 3. Kiểm tra thời gian sản xuất còn lại
            if (prod.ValueRO.TimeRemaining > 0f)
            {
                continue;
            }

            Entity unitPrefab = queueBuffer[0].UnitPrefab;

            // 4. Kiểm tra Prefab trong hàng đợi có bị Null không
            if (unitPrefab == Entity.Null)
            {
                UnityEngine.Debug.LogError("ProductionSystem: UnitPrefab in queue is NULL. Removing from queue.");
                queueBuffer.RemoveAt(0);
                continue;
            }

            // 5. Kiểm tra xem nhà này có dữ liệu Component Unit không (để biết ai là chủ sở hữu)
            if (!unitLookup.HasComponent(entity))
            {
                UnityEngine.Debug.LogError("ProductionSystem: Building entity missing Unit component (Cannot determine playerID).");
                continue;
            }
            var untiComponentOfBuilding = unitLookup[entity];

            // 6. Kiểm tra giới hạn dân số (Population)
            PlayerContext playerContextEntity = new PlayerContext();
            PlayerContextHelper.GetContextData(state.EntityManager, untiComponentOfBuilding.playerID, out playerContextEntity);

            if (playerContextEntity.currentPopulation >= playerContextEntity.maxPopulation)
            {
                UnityEngine.Debug.LogWarning($"ProductionSystem: Max population reached ({playerContextEntity.currentPopulation}/{playerContextEntity.maxPopulation}). Production paused.");
                continue;
            }

            // --- BẮT ĐẦU INSTANTIATE UNIT KHI CÁC ĐIỀU KIỆN TRÊN ĐỀU THỎA MÃN ---
            UnityEngine.Debug.Log($"ProductionSystem: Spawning unit from prefab Index 0 for Player {untiComponentOfBuilding.playerID}");

            Entity unit = ecb.Instantiate(unitPrefab);

            PlayerContextHelper.UpdatePlayerContext(state.EntityManager, playerContextEntity.PlayerId, PlayerContextDataType.currentPopulation, playerContextEntity.currentPopulation + 1);

            float3 spawnPos = buildingTransform.ValueRO.TransformPoint(prod.ValueRO.SpawnOffset);
            float3 rallyPos = buildingTransform.ValueRO.TransformPoint(prod.ValueRO.RallyOffset);

            var unitComponentOfPrefab = unitLookup[unitPrefab];

            ecb.SetComponent(unit, new Unit
            {
                playerID = untiComponentOfBuilding.playerID,
                unitName = unitComponentOfPrefab.unitName
            });

            ecb.SetComponent(unit, new Selectable { playerID = untiComponentOfBuilding.playerID });

            ecb.SetComponent(
                unit,
                LocalTransform.FromPositionRotationScale(
                    spawnPos,
                    quaternion.identity,
                    1f
                )
            );

            ApplyRallyMoveOverride(
                moveOverrideLookup,
                ecb,
                unit,
                unitPrefab,
                rallyPos
            );

            queueBuffer.RemoveAt(0);

            prod.ValueRW.TimeRemaining = queueBuffer.IsEmpty ? 0f : prod.ValueRO.ProductionTime;
        }
    }

    [BurstCompile]
    private void ApplyRallyMoveOverride(
        ComponentLookup<MoveOverride> moveOverrideLookup,
        EntityCommandBuffer ecb,
        Entity unit,
        Entity unitPrefab,
        float3 rallyPos)
    {
        if (!moveOverrideLookup.HasComponent(unitPrefab))
        {
            UnityEngine.Debug.LogWarning("ProductionSystem: Produced unit prefab is missing MoveOverride component.");
            return;
        }

        MoveOverride moveOverride = moveOverrideLookup[unitPrefab];
        moveOverride.targetPosition = rallyPos;
        moveOverride.targetApplied = false;

        ecb.SetComponent(unit, moveOverride);
        ecb.SetComponentEnabled<MoveOverride>(unit, true);
    }
}