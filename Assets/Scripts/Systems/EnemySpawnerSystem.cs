using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine; 

partial struct EnemySpawnerSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntitiesReferences entitiesReferences = SystemAPI.GetSingleton<EntitiesReferences>();
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
        }
    }
}