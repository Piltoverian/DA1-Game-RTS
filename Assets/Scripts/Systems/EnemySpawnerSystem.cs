using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine; 

partial struct EnemySpawnerSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EntitiesReferences>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntitiesReferences entitiesReferences = SystemAPI.GetSingleton<EntitiesReferences>();
        EntityCommandBuffer ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        foreach ((RefRO<LocalTransform> localTransform, RefRW<EnemySpawner> enemySpawner)
            in SystemAPI.Query<RefRO<LocalTransform>, RefRW<EnemySpawner>>())
        {
            enemySpawner.ValueRW.timer -= SystemAPI.Time.DeltaTime;

            if (enemySpawner.ValueRO.timer > 0f)
            {
                continue;
            }
            enemySpawner.ValueRW.timer = enemySpawner.ValueRO.timerMax;

            Entity enemyEntity = state.EntityManager.Instantiate(entitiesReferences.enemyPrefab);
            //Debug.Log("Spawned enemy: " + enemyEntity);

            SystemAPI.SetComponent(enemyEntity,
                LocalTransform.FromPosition(localTransform.ValueRO.Position));
            var grid= SystemAPI.GetSingleton<GridComponent>();
            if (SystemAPI.HasComponent<MovementAgentComponent>(enemyEntity))
            {
                MovementAgentAPI.SetTarget(state.EntityManager,enemyEntity ,enemySpawner.ValueRO.RallyPoint, grid, ecb);
            }
        }
        ecb.Playback(state.EntityManager);
    }
}