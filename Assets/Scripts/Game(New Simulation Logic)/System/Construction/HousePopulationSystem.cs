using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Grants the house population cap once the building has finished construction.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ConstructionSystem))]
public partial struct HousePopulationSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerContext>();
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (house, building, entity) in
                 SystemAPI.Query<RefRO<HouseData>, RefRO<BuildingData>>()
                     .WithNone<UnderConstructionTag, HousePopulationAppliedTag>()
                     .WithEntityAccess())
        {
            int amount = math.max(0, house.ValueRO.MaxPopulationIncrease);
            int playerId = building.ValueRO.PlayerID;

            PlayerContext context;
            FunctionResult getResult = PlayerContextHelper.GetContextData(
                entityManager,
                playerId,
                out context
            );

            if (getResult == FunctionResult.Failure)
            {
                Debug.LogWarning(
                    $"HousePopulationSystem: PlayerContext not found for PlayerID={playerId}. House={entity}."
                );
                continue;
            }

            FunctionResult updateResult = PlayerContextHelper.UpdatePlayerContext(
                entityManager,
                playerId,
                PlayerContextDataType.maxPopulation,
                context.maxPopulation + amount
            );

            if (updateResult == FunctionResult.Failure)
            {
                Debug.LogWarning(
                    $"HousePopulationSystem: Cannot update maxPopulation for PlayerID={playerId}. House={entity}."
                );
                continue;
            }

            HousePopulationCleanup cleanup = entityManager.HasComponent<HousePopulationCleanup>(entity)
                ? entityManager.GetComponentData<HousePopulationCleanup>(entity)
                : default;

            cleanup.PlayerId = playerId;
            cleanup.Amount = amount;
            cleanup.WasApplied = 1;

            if (entityManager.HasComponent<HousePopulationCleanup>(entity))
                entityManager.SetComponentData(entity, cleanup);
            else
                ecb.AddComponent(entity, cleanup);

            ecb.AddComponent<HousePopulationAppliedTag>(entity);

            Debug.Log(
                $"HousePopulationSystem: House={entity}, PlayerID={playerId}, MaxPopulation {context.maxPopulation} -> {context.maxPopulation + amount}."
            );
        }

        ecb.Playback(entityManager);
        ecb.Dispose();
    }
}

/// <summary>
/// Removes the granted population cap after a completed house is destroyed.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct HousePopulationCleanupSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

        // After DestroyEntity, normal components are removed while cleanup
        // components remain. An alive house still has HouseData and is ignored.
        foreach (var (cleanup, entity) in
                 SystemAPI.Query<RefRO<HousePopulationCleanup>>()
                     .WithNone<HouseData>()
                     .WithEntityAccess())
        {
            if (cleanup.ValueRO.WasApplied != 0)
            {
                PlayerContext context;
                FunctionResult getResult = PlayerContextHelper.GetContextData(
                    entityManager,
                    cleanup.ValueRO.PlayerId,
                    out context
                );

                if (getResult == FunctionResult.Success)
                {
                    int newMaxPopulation = math.max(
                        0,
                        context.maxPopulation - cleanup.ValueRO.Amount
                    );

                    PlayerContextHelper.UpdatePlayerContext(
                        entityManager,
                        cleanup.ValueRO.PlayerId,
                        PlayerContextDataType.maxPopulation,
                        newMaxPopulation
                    );

                    Debug.Log(
                        $"HousePopulationCleanupSystem: House={entity}, PlayerID={cleanup.ValueRO.PlayerId}, MaxPopulation {context.maxPopulation} -> {newMaxPopulation}."
                    );
                }
            }

            // Final removal lets ECS fully delete the residual cleanup entity.
            ecb.RemoveComponent<HousePopulationCleanup>(entity);
        }

        ecb.Playback(entityManager);
        ecb.Dispose();
    }
}
