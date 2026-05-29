using Unity.Burst;
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

        foreach (var (prod, buildingTransform, entity) in
                 SystemAPI.Query<
                         RefRW<ProductionData>,
                         RefRO<LocalTransform>>()
                     .WithNone<UnderConstructionTag>()
                     .WithEntityAccess())
        {
            DynamicBuffer<ProductionQueueElement> queueBuffer =
                state.EntityManager.GetBuffer<ProductionQueueElement>(entity);

            if (queueBuffer.IsEmpty)
                continue;

            if (prod.ValueRO.TimeRemaining <= 0f)
            {
                prod.ValueRW.TimeRemaining = prod.ValueRO.ProductionTime;
            }

            prod.ValueRW.TimeRemaining -= dt;

            if (prod.ValueRO.TimeRemaining > 0f)
                continue;

            Entity unitPrefab = queueBuffer[0].UnitPrefab;

            if (unitPrefab == Entity.Null)
            {
                //Debug.LogError("UnitPrefab is NULL");
                queueBuffer.RemoveAt(0);
                continue;
            }

            Entity unit = ecb.Instantiate(unitPrefab);

            float3 spawnPos =
                buildingTransform.ValueRO.TransformPoint(prod.ValueRO.SpawnOffset);

            float3 rallyPos =
                buildingTransform.ValueRO.TransformPoint(prod.ValueRO.RallyOffset);

            ecb.SetComponent(
                unit,
                LocalTransform.FromPositionRotationScale(
                    spawnPos,
                    quaternion.identity,
                    1f
                )
            );

            ApplyRallyMoveOverride(
                ref state,
                ecb,
                unit,
                unitPrefab,
                rallyPos
            );

            queueBuffer.RemoveAt(0);

            prod.ValueRW.TimeRemaining =
                queueBuffer.IsEmpty
                    ? 0f
                    : prod.ValueRO.ProductionTime;
        }
    }

    private void ApplyRallyMoveOverride(
        ref SystemState state,
        EntityCommandBuffer ecb,
        Entity unit,
        Entity unitPrefab,
        float3 rallyPos)
    {
        if (!state.EntityManager.HasComponent<MoveOverride>(unitPrefab))
        {
            //Debug.LogError(
            //    "Produced unit prefab has no MoveOverride component. " +
            //    "Add MoveOverrideAuthoring to the unit prefab."
            //);

            return;
        }

        MoveOverride moveOverride =
            state.EntityManager.GetComponentData<MoveOverride>(unitPrefab);

        moveOverride.targetPosition = rallyPos;
        moveOverride.targetApplied = false;

        ecb.SetComponent(unit, moveOverride);
        ecb.SetComponentEnabled<MoveOverride>(unit, true);
    }
}