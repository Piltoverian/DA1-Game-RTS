using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public enum EnemySpawnSelectionMode
{
    /// <summary>
    /// Spawn only the strongest currently unlocked type.
    /// Example: weak -> medium -> strong.
    /// </summary>
    CurrentUnlockedOnly = 0,

    /// <summary>
    /// Cycle through all types unlocked so far.
    /// Example after unlocking medium: weak -> medium -> weak -> medium.
    /// </summary>
    CycleUnlockedTypes = 1
}

public class EnemySpawnerAuthoring : MonoBehaviour
{
    [Header("Enemy Prefabs - Order Matters")]
    [Tooltip("Add prefabs in order: weak enemy first, stronger enemies later.")]
    public List<GameObject> enemyPrefabs = new List<GameObject>();

    [Header("Spawn Timing")]
    [Min(0.01f)]
    public float spawnInterval = 2f;

    [Min(1)]
    public int spawnAmountPerTick = 1;

    [Tooltip("After this amount of time, unlock the next prefab in Enemy Prefabs.")]
    [Min(0.01f)]
    public float unlockNextEnemyTypeInterval = 15f;

    public bool spawnFirstEnemyImmediately = true;

    [Header("Spawn Type Selection")]
    public EnemySpawnSelectionMode spawnSelectionMode =
        EnemySpawnSelectionMode.CurrentUnlockedOnly;

    [Header("Spawn Formation")]
    [Tooltip("Distance between newly spawned enemies. Increase this when their avoidance radius is large.")]
    [Min(0.1f)]
    public float spawnSpacing = 2f;

    [Tooltip("Number of positions per row around the spawner.")]
    [Min(1)]
    public int spawnColumns = 3;

    [Tooltip("Number of rows behind the spawner. Spawn slots are reused after all rows are used.")]
    [Min(1)]
    public int spawnRows = 2;

    [Header("Enemy Main Base Target")]
    [Tooltip("Player ID of the opponent main base. MainBaseAuthoring and BuildingAuthoring must be attached to that base.")]
    public int targetPlayerID = 1;

    [Tooltip("Stop before the base center so the target is outside the blocked building footprint.")]
    [Min(0f)]
    public float stopBeforeMainBaseDistance = 4f;

    [Tooltip("Use this world point only when no tagged main base for Target Player ID is found.")]
    public bool useFallbackTarget = false;

    public Vector3 fallbackTarget;

    [Header("Debug")]
    [Tooltip("Print important warnings such as missing base, invalid prefab, missing MovementAgentComponent and invalid target.")]
    public bool logSpawnFailures = true;

    [Tooltip("Print the main-base scan result once and print information for each spawned enemy.")]
    public bool enableDetailedDebug = true;

    [Tooltip("How many seconds each newly spawned enemy should be tracked after spawning.")]
    [Min(0.1f)]
    public float debugTrackDuration = 6f;

    [Tooltip("How often the movement state of a newly spawned enemy is printed.")]
    [Min(0.05f)]
    public float debugLogInterval = 0.5f;

    public class Baker : Baker<EnemySpawnerAuthoring>
    {
        public override void Bake(EnemySpawnerAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            float safeSpawnInterval = math.max(0.01f, authoring.spawnInterval);
            float safeUnlockInterval = math.max(0.01f, authoring.unlockNextEnemyTypeInterval);

            AddComponent(entity, new EnemySpawner
            {
                spawnTimer = authoring.spawnFirstEnemyImmediately
                    ? 0f
                    : safeSpawnInterval,
                spawnInterval = safeSpawnInterval,

                unlockTimer = safeUnlockInterval,
                unlockNextEnemyTypeInterval = safeUnlockInterval,

                currentEnemyIndex = 0,
                nextCycleSpawnIndex = 0,

                spawnAmountPerTick = math.max(1, authoring.spawnAmountPerTick),
                spawnSpacing = math.max(0.1f, authoring.spawnSpacing),
                spawnColumns = math.max(1, authoring.spawnColumns),
                spawnRows = math.max(1, authoring.spawnRows),
                nextSpawnSlot = 0,

                targetPlayerID = authoring.targetPlayerID,
                stopBeforeMainBaseDistance = math.max(0f, authoring.stopBeforeMainBaseDistance),
                useFallbackTarget = authoring.useFallbackTarget,
                fallbackTarget = authoring.fallbackTarget,

                spawnSelectionMode = authoring.spawnSelectionMode,

                logSpawnFailures = authoring.logSpawnFailures,
                enableDetailedDebug = authoring.enableDetailedDebug,
                debugTrackDuration = math.max(0.1f, authoring.debugTrackDuration),
                debugLogInterval = math.max(0.05f, authoring.debugLogInterval),

                hasWarnedMissingTarget = false,
                hasLoggedBaseScan = false,
                hasLoggedResolvedDestination = false
            });

            DynamicBuffer<EnemySpawnPrefabElement> buffer =
                AddBuffer<EnemySpawnPrefabElement>(entity);

            if (authoring.enemyPrefabs == null)
                return;

            for (int i = 0; i < authoring.enemyPrefabs.Count; i++)
            {
                GameObject prefab = authoring.enemyPrefabs[i];

                if (prefab == null)
                    continue;

                buffer.Add(new EnemySpawnPrefabElement
                {
                    prefab = GetEntity(prefab, TransformUsageFlags.Dynamic)
                });
            }
        }
    }
}

public struct EnemySpawner : IComponentData
{
    public float spawnTimer;
    public float spawnInterval;

    public float unlockTimer;
    public float unlockNextEnemyTypeInterval;

    public int currentEnemyIndex;
    public int nextCycleSpawnIndex;

    public int spawnAmountPerTick;
    public float spawnSpacing;
    public int spawnColumns;
    public int spawnRows;
    public int nextSpawnSlot;

    public int targetPlayerID;
    public float stopBeforeMainBaseDistance;
    public bool useFallbackTarget;
    public float3 fallbackTarget;

    public EnemySpawnSelectionMode spawnSelectionMode;

    public bool logSpawnFailures;
    public bool enableDetailedDebug;
    public float debugTrackDuration;
    public float debugLogInterval;

    public bool hasWarnedMissingTarget;
    public bool hasLoggedBaseScan;
    public bool hasLoggedResolvedDestination;
}

public struct EnemySpawnPrefabElement : IBufferElementData
{
    public Entity prefab;
}

/// <summary>
/// Temporary runtime-only component used to print the movement state of a newly
/// spawned enemy. EnemySpawnMoveDebugSystem removes it automatically.
/// </summary>
public struct EnemySpawnMoveDebug : IComponentData
{
    public float elapsed;
    public float nextLogTime;
    public float logInterval;
    public float duration;

    public float3 spawnPosition;
    public float3 expectedTarget;

    public bool warnedTargetLost;
}
