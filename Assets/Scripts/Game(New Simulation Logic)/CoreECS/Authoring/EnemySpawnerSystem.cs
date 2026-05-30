using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Spawns enemy types sequentially and immediately sends every spawned enemy
/// toward the opponent main base.
///
/// Important: this system runs before MovementAgentPathRequestSystem inside the
/// FixedStepSimulationSystemGroup. Therefore the newly assigned target can be
/// processed by the flow-field pipeline during the same fixed step.
/// </summary>
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(GridInitSystem))]
[UpdateBefore(typeof(MovementAgentPathRequestSystem))]
public partial struct EnemySpawnerSystem : ISystem
{
    private EntityQuery m_SpawnerQuery;

    private struct MainBaseInfo
    {
        public Entity entity;
        public int playerID;
        public float3 position;
    }

    private struct SpawnPostPlaybackCheck
    {
        public Entity enemyEntity;
        public float3 expectedTarget;
        public TargetChangeResult setTargetResult;
    }

    public void OnCreate(ref SystemState state)
    {
        m_SpawnerQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<LocalTransform>()
            .WithAllRW<EnemySpawner>()
            .WithAll<EnemySpawnPrefabElement>()
            .Build(ref state);

        state.RequireForUpdate(m_SpawnerQuery);
        state.RequireForUpdate<GridComponent>();
    }

    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        EntityManager entityManager = state.EntityManager;

        GridComponent grid = SystemAPI.GetSingleton<GridComponent>();
        Entity gridEntity = SystemAPI.GetSingletonEntity<GridComponent>();

        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

        NativeList<MainBaseInfo> mainBases =
            new NativeList<MainBaseInfo>(Allocator.Temp);

        NativeList<SpawnPostPlaybackCheck> postPlaybackChecks =
            new NativeList<SpawnPostPlaybackCheck>(Allocator.Temp);

        foreach (var (building, transform, entity) in
                 SystemAPI.Query<RefRO<BuildingData>, RefRO<LocalTransform>>()
                     .WithAll<MainBaseTag>()
                     .WithEntityAccess())
        {
            mainBases.Add(new MainBaseInfo
            {
                entity = entity,
                playerID = building.ValueRO.PlayerID,
                position = transform.ValueRO.Position
            });
        }

        // IMPORTANT: take a snapshot before spawning. EntityManager.Instantiate,
        // RemoveComponent and AddComponentData are structural changes. Performing
        // them directly while a SystemAPI.Query foreach is active throws:
        // "Structural changes are not allowed while iterating over entities".
        NativeArray<Entity> spawnerEntities =
            m_SpawnerQuery.ToEntityArray(Allocator.Temp);

        for (int spawnerIndex = 0; spawnerIndex < spawnerEntities.Length; spawnerIndex++)
        {
            Entity spawnerEntity = spawnerEntities[spawnerIndex];

            if (!entityManager.Exists(spawnerEntity))
                continue;

            LocalTransform spawnerTransform =
                entityManager.GetComponentData<LocalTransform>(spawnerEntity);

            EnemySpawner spawner =
                entityManager.GetComponentData<EnemySpawner>(spawnerEntity);

            DynamicBuffer<EnemySpawnPrefabElement> prefabBuffer =
                entityManager.GetBuffer<EnemySpawnPrefabElement>(spawnerEntity);

            LogMainBaseScanOnce(
                entityManager,
                gridEntity,
                grid,
                spawnerEntity,
                mainBases,
                ref spawner);

            if (prefabBuffer.Length == 0)
            {
                if (spawner.logSpawnFailures)
                {
                    Debug.LogWarning(
                        $"[EnemySpawner][NoPrefab] Spawner={spawnerEntity}. " +
                        "EnemySpawnPrefabElement buffer is empty. Add enemy prefabs in EnemySpawnerAuthoring.");
                }

                entityManager.SetComponentData(spawnerEntity, spawner);
                continue;
            }

            UpdateUnlockedEnemyType(
                ref spawner,
                prefabBuffer.Length,
                deltaTime);

            spawner.spawnTimer -= deltaTime;

            if (spawner.spawnTimer > 0f)
            {
                entityManager.SetComponentData(spawnerEntity, spawner);
                continue;
            }

            spawner.spawnTimer = spawner.spawnInterval;

            if (!TryGetDestination(
                    mainBases,
                    spawnerTransform.Position,
                    ref spawner,
                    out float3 destination,
                    out MainBaseInfo selectedBase,
                    out bool usedFallbackTarget))
            {
                entityManager.SetComponentData(spawnerEntity, spawner);
                continue;
            }

            LogResolvedDestinationOnce(
                entityManager,
                gridEntity,
                grid,
                spawnerEntity,
                selectedBase,
                usedFallbackTarget,
                destination,
                ref spawner);

            int amount = math.max(1, spawner.spawnAmountPerTick);

            for (int i = 0; i < amount; i++)
            {
                SpawnOneEnemy(
                    ref spawner,
                    spawnerEntity,
                    spawnerTransform.Position,
                    destination,
                    prefabBuffer,
                    entityManager,
                    grid,
                    gridEntity,
                    ecb,
                    ref postPlaybackChecks);
            }

            entityManager.SetComponentData(spawnerEntity, spawner);
        }

        spawnerEntities.Dispose();

        // SetTarget writes MovementAgentComponent through ECB. Playback here is
        // intentionally before MovementAgentPathRequestSystem executes.
        ecb.Playback(entityManager);
        ecb.Dispose();

        LogPostPlaybackState(entityManager, postPlaybackChecks);

        postPlaybackChecks.Dispose();
        mainBases.Dispose();
    }

    private static void SpawnOneEnemy(
        ref EnemySpawner spawner,
        Entity spawnerEntity,
        float3 spawnerPosition,
        float3 destination,
        DynamicBuffer<EnemySpawnPrefabElement> prefabBuffer,
        EntityManager entityManager,
        GridComponent grid,
        Entity gridEntity,
        EntityCommandBuffer ecb,
        ref NativeList<SpawnPostPlaybackCheck> postPlaybackChecks)
    {
        int prefabIndex = GetNextPrefabIndex(ref spawner, prefabBuffer.Length);
        Entity prefab = prefabBuffer[prefabIndex].prefab;

        if (prefab == Entity.Null || !entityManager.Exists(prefab))
        {
            if (spawner.logSpawnFailures)
            {
                Debug.LogWarning(
                    $"[EnemySpawner][InvalidPrefab] Spawner={spawnerEntity}, PrefabIndex={prefabIndex}, Prefab={prefab}.");
            }

            return;
        }

        Entity enemyEntity = entityManager.Instantiate(prefab);

        float3 spawnPosition = GetNextSpawnPosition(
            ref spawner,
            spawnerPosition,
            destination);

        if (entityManager.HasComponent<LocalTransform>(enemyEntity))
        {
            LocalTransform transform =
                entityManager.GetComponentData<LocalTransform>(enemyEntity);

            transform.Position = spawnPosition;
            entityManager.SetComponentData(enemyEntity, transform);
        }
        else if (spawner.logSpawnFailures)
        {
            Debug.LogWarning(
                $"[EnemySpawner][MissingLocalTransform] Enemy={enemyEntity}, PrefabIndex={prefabIndex}.");
        }

        bool hadDefaultPositionSetup =
            entityManager.HasComponent<SetupUnitMoverDefaultPosition>(enemyEntity);

        bool hadMoveOverride =
            entityManager.HasComponent<MoveOverride>(enemyEntity);

        bool moveOverrideWasEnabled =
            hadMoveOverride && entityManager.IsComponentEnabled<MoveOverride>(enemyEntity);

        ResetSpawnedEnemyState(entityManager, enemyEntity);

        bool hasMovementAgent =
            entityManager.HasComponent<MovementAgentComponent>(enemyEntity);

        bool hasSteering =
            entityManager.HasComponent<MovementSteeringComponent>(enemyEntity);

        int2 spawnCell = GridHelper.WorldToGrid(spawnPosition, grid);
        int2 destinationCell = GridHelper.WorldToGrid(destination, grid);

        bool spawnInsideGrid = IsInsideGrid(spawnCell, grid);
        bool destinationInsideGrid = IsInsideGrid(destinationCell, grid);
        int destinationCost = GetGridCost(entityManager, gridEntity, grid, destinationCell);

        if (spawner.enableDetailedDebug)
        {
            Debug.Log(
                $"[EnemySpawner][SpawnAttempt] Spawner={spawnerEntity}, Enemy={enemyEntity}, PrefabIndex={prefabIndex}, " +
                $"Spawn={Format(spawnPosition)}, SpawnCell={Format(spawnCell)}, SpawnInsideGrid={spawnInsideGrid}, " +
                $"Destination={Format(destination)}, DestinationCell={Format(destinationCell)}, " +
                $"DestinationInsideGrid={destinationInsideGrid}, DestinationCost={destinationCost}, " +
                $"HasMovementAgent={hasMovementAgent}, HasSteering={hasSteering}, " +
                $"RemovedSetupUnitMoverDefaultPosition={hadDefaultPositionSetup}, " +
                $"HadMoveOverride={hadMoveOverride}, MoveOverrideWasEnabled={moveOverrideWasEnabled}.");
        }

        if (!spawnInsideGrid)
        {
            Debug.LogWarning(
                $"[EnemySpawner][SpawnOutsideGrid] Enemy={enemyEntity}, Spawn={Format(spawnPosition)}, SpawnCell={Format(spawnCell)}. " +
                "Move the spawner inside the GridComponent area or reduce Spawn Spacing / Spawn Rows.");
        }

        if (!destinationInsideGrid)
        {
            if (spawner.logSpawnFailures)
            {
                Debug.LogWarning(
                    $"[EnemySpawner][DestinationOutsideGrid] Enemy={enemyEntity}, Destination={Format(destination)}, " +
                    $"DestinationCell={Format(destinationCell)}. SetTarget will return InvalidTarget.");
            }

            return;
        }

        if (destinationCost == int.MaxValue && spawner.logSpawnFailures)
        {
            Debug.LogWarning(
                $"[EnemySpawner][BlockedDestination] Enemy={enemyEntity}, Destination={Format(destination)}, " +
                $"DestinationCell={Format(destinationCell)} has GridNodeCost=int.MaxValue. " +
                "Increase Stop Before Main Base Distance so the target is outside the building footprint.");
        }

        if (!hasMovementAgent)
        {
            if (spawner.logSpawnFailures)
            {
                Debug.LogWarning(
                    $"[EnemySpawner][MissingMovementAgent] Enemy={enemyEntity}. " +
                    "Add MovementAgentAuthoring to the enemy prefab.");
            }

            return;
        }

        if (!hasSteering && spawner.logSpawnFailures)
        {
            Debug.LogWarning(
                $"[EnemySpawner][MissingSteering] Enemy={enemyEntity}. " +
                "MovementAgentTargetSystem requires MovementSteeringComponent. " +
                "Check MovementAgentAuthoring on the prefab.");
        }

        TargetChangeResult result = MovementAgentAPI.SetTarget(
            entityManager,
            enemyEntity,
            destination,
            grid,
            ecb);

        if (spawner.enableDetailedDebug ||
            (result != TargetChangeResult.Success && spawner.logSpawnFailures))
        {
            Debug.Log(
                $"[EnemySpawner][SetTarget] Enemy={enemyEntity}, Result={result}, " +
                $"Destination={Format(destination)}, DestinationCell={Format(destinationCell)}.");
        }

        if (spawner.enableDetailedDebug)
        {
            EnemySpawnMoveDebug debugData = new EnemySpawnMoveDebug
            {
                elapsed = 0f,
                nextLogTime = 0f,
                logInterval = math.max(0.05f, spawner.debugLogInterval),
                duration = math.max(0.1f, spawner.debugTrackDuration),
                spawnPosition = spawnPosition,
                expectedTarget = destination,
                warnedTargetLost = false
            };

            if (entityManager.HasComponent<EnemySpawnMoveDebug>(enemyEntity))
            {
                entityManager.SetComponentData(enemyEntity, debugData);
            }
            else
            {
                entityManager.AddComponentData(enemyEntity, debugData);
            }
        }

        postPlaybackChecks.Add(new SpawnPostPlaybackCheck
        {
            enemyEntity = enemyEntity,
            expectedTarget = destination,
            setTargetResult = result
        });
    }

    private static void ResetSpawnedEnemyState(
        EntityManager entityManager,
        Entity enemyEntity)
    {
        // Useful for units placed directly in a scene, but incorrect for runtime
        // enemy spawns because the setup system would overwrite our destination
        // with the spawn position during a later initialization update.
        if (entityManager.HasComponent<SetupUnitMoverDefaultPosition>(enemyEntity))
        {
            entityManager.RemoveComponent<SetupUnitMoverDefaultPosition>(enemyEntity);
        }

        // A prefab must not inherit an enabled player-issued MoveOverride.
        if (entityManager.HasComponent<MoveOverride>(enemyEntity))
        {
            MoveOverride moveOverride =
                entityManager.GetComponentData<MoveOverride>(enemyEntity);

            moveOverride.targetApplied = false;
            entityManager.SetComponentData(enemyEntity, moveOverride);
            entityManager.SetComponentEnabled<MoveOverride>(enemyEntity, false);
        }

        // Combat targeting will be reacquired normally after spawning.
        if (entityManager.HasComponent<Target>(enemyEntity))
        {
            Target target = entityManager.GetComponentData<Target>(enemyEntity);
            target.targetEntity = Entity.Null;
            entityManager.SetComponentData(enemyEntity, target);
        }
    }

    private static void LogPostPlaybackState(
        EntityManager entityManager,
        NativeList<SpawnPostPlaybackCheck> postPlaybackChecks)
    {
        for (int i = 0; i < postPlaybackChecks.Length; i++)
        {
            SpawnPostPlaybackCheck check = postPlaybackChecks[i];

            if (!entityManager.Exists(check.enemyEntity))
            {
                Debug.LogWarning(
                    $"[EnemySpawner][AfterPlayback] Enemy={check.enemyEntity} no longer exists.");
                continue;
            }

            if (!entityManager.HasComponent<MovementAgentComponent>(check.enemyEntity))
            {
                Debug.LogWarning(
                    $"[EnemySpawner][AfterPlayback] Enemy={check.enemyEntity} still has no MovementAgentComponent.");
                continue;
            }

            MovementAgentComponent move =
                entityManager.GetComponentData<MovementAgentComponent>(check.enemyEntity);

            Debug.Log(
                $"[EnemySpawner][AfterPlayback] Enemy={check.enemyEntity}, SetTargetResult={check.setTargetResult}, " +
                $"HasTarget={move.hastarget}, CurrentWorldTarget={Format(move.currentworldtarget)}, " +
                $"ExpectedTarget={Format(check.expectedTarget)}, FieldEntity={move.FieldEntity}, " +
                $"HasTargetChangeRequest={entityManager.HasComponent<TargetChangeRequest>(check.enemyEntity)}. " +
                "Expected immediately after playback: HasTarget=True and FieldEntity may still be Entity.Null until the flow-field request pipeline runs.");
        }
    }

    private static void LogMainBaseScanOnce(
        EntityManager entityManager,
        Entity gridEntity,
        GridComponent grid,
        Entity spawnerEntity,
        NativeList<MainBaseInfo> mainBases,
        ref EnemySpawner spawner)
    {
        if (!spawner.enableDetailedDebug || spawner.hasLoggedBaseScan)
            return;

        spawner.hasLoggedBaseScan = true;

        Debug.Log(
            $"[EnemySpawner][BaseScan] Spawner={spawnerEntity}, TargetPlayerID={spawner.targetPlayerID}, " +
            $"FoundMainBaseCount={mainBases.Length}, UseFallbackTarget={spawner.useFallbackTarget}.");

        for (int i = 0; i < mainBases.Length; i++)
        {
            int2 cell = GridHelper.WorldToGrid(mainBases[i].position, grid);
            int cost = GetGridCost(entityManager, gridEntity, grid, cell);

            Debug.Log(
                $"[EnemySpawner][BaseScan] MainBase[{i}] Entity={mainBases[i].entity}, " +
                $"PlayerID={mainBases[i].playerID}, Position={Format(mainBases[i].position)}, " +
                $"Cell={Format(cell)}, CellCost={cost}.");
        }
    }

    private static void LogResolvedDestinationOnce(
        EntityManager entityManager,
        Entity gridEntity,
        GridComponent grid,
        Entity spawnerEntity,
        MainBaseInfo selectedBase,
        bool usedFallbackTarget,
        float3 destination,
        ref EnemySpawner spawner)
    {
        if (!spawner.enableDetailedDebug || spawner.hasLoggedResolvedDestination)
            return;

        spawner.hasLoggedResolvedDestination = true;

        int2 destinationCell = GridHelper.WorldToGrid(destination, grid);
        int destinationCost = GetGridCost(entityManager, gridEntity, grid, destinationCell);

        Debug.Log(
            $"[EnemySpawner][ResolvedDestination] Spawner={spawnerEntity}, UsedFallback={usedFallbackTarget}, " +
            $"SelectedBaseEntity={selectedBase.entity}, SelectedBasePlayerID={selectedBase.playerID}, " +
            $"SelectedBasePosition={Format(selectedBase.position)}, Destination={Format(destination)}, " +
            $"DestinationCell={Format(destinationCell)}, InsideGrid={IsInsideGrid(destinationCell, grid)}, " +
            $"DestinationCost={destinationCost}, StopBeforeMainBaseDistance={spawner.stopBeforeMainBaseDistance:F2}.");
    }

    private static bool TryGetDestination(
        NativeList<MainBaseInfo> mainBases,
        float3 spawnerPosition,
        ref EnemySpawner spawner,
        out float3 destination,
        out MainBaseInfo selectedBase,
        out bool usedFallbackTarget)
    {
        for (int i = 0; i < mainBases.Length; i++)
        {
            if (mainBases[i].playerID != spawner.targetPlayerID)
                continue;

            selectedBase = mainBases[i];
            usedFallbackTarget = false;

            float3 towardBase = selectedBase.position - spawnerPosition;
            towardBase.y = 0f;

            float3 direction = math.normalizesafe(
                towardBase,
                new float3(0f, 0f, 1f));

            destination =
                selectedBase.position -
                direction * spawner.stopBeforeMainBaseDistance;

            spawner.hasWarnedMissingTarget = false;
            return true;
        }

        if (spawner.useFallbackTarget)
        {
            selectedBase = default;
            usedFallbackTarget = true;
            destination = spawner.fallbackTarget;
            spawner.hasWarnedMissingTarget = false;
            return true;
        }

        selectedBase = default;
        usedFallbackTarget = false;
        destination = default;

        if (!spawner.hasWarnedMissingTarget && spawner.logSpawnFailures)
        {
            Debug.LogWarning(
                $"[EnemySpawner][MissingMainBase] Cannot find an entity with MainBaseTag and " +
                $"BuildingData.PlayerID={spawner.targetPlayerID}. Attach MainBaseAuthoring and " +
                "BuildingAuthoring to the SAME main-base GameObject, or enable Use Fallback Target.");
        }

        spawner.hasWarnedMissingTarget = true;
        return false;
    }

    private static float3 GetNextSpawnPosition(
        ref EnemySpawner spawner,
        float3 spawnerPosition,
        float3 destination)
    {
        int columns = math.max(1, spawner.spawnColumns);
        int rows = math.max(1, spawner.spawnRows);
        int capacity = math.max(1, columns * rows);

        int slot = spawner.nextSpawnSlot % capacity;
        spawner.nextSpawnSlot = (slot + 1) % capacity;

        int column = slot % columns;
        int row = slot / columns;

        float centeredColumn =
            column - (columns - 1) * 0.5f;

        float3 forward = destination - spawnerPosition;
        forward.y = 0f;
        forward = math.normalizesafe(forward, new float3(0f, 0f, 1f));

        float3 right = new float3(forward.z, 0f, -forward.x);

        return spawnerPosition
               + right * centeredColumn * spawner.spawnSpacing
               - forward * row * spawner.spawnSpacing;
    }

    private static void UpdateUnlockedEnemyType(
        ref EnemySpawner spawner,
        int enemyTypeCount,
        float deltaTime)
    {
        if (enemyTypeCount <= 1)
        {
            spawner.currentEnemyIndex = 0;
            spawner.nextCycleSpawnIndex = 0;
            return;
        }

        if (spawner.currentEnemyIndex >= enemyTypeCount - 1)
            return;

        spawner.unlockTimer -= deltaTime;

        if (spawner.unlockTimer > 0f)
            return;

        spawner.unlockTimer = spawner.unlockNextEnemyTypeInterval;
        spawner.currentEnemyIndex++;

        if (spawner.currentEnemyIndex >= enemyTypeCount)
            spawner.currentEnemyIndex = enemyTypeCount - 1;

        if (spawner.nextCycleSpawnIndex > spawner.currentEnemyIndex)
            spawner.nextCycleSpawnIndex = 0;
    }

    private static int GetNextPrefabIndex(
        ref EnemySpawner spawner,
        int prefabCount)
    {
        if (prefabCount <= 0)
            return 0;

        int maxUnlockedIndex = math.clamp(
            spawner.currentEnemyIndex,
            0,
            prefabCount - 1);

        if (spawner.spawnSelectionMode ==
            EnemySpawnSelectionMode.CurrentUnlockedOnly)
        {
            return maxUnlockedIndex;
        }

        int result = math.clamp(
            spawner.nextCycleSpawnIndex,
            0,
            maxUnlockedIndex);

        spawner.nextCycleSpawnIndex++;

        if (spawner.nextCycleSpawnIndex > maxUnlockedIndex)
            spawner.nextCycleSpawnIndex = 0;

        return result;
    }

    private static int GetGridCost(
        EntityManager entityManager,
        Entity gridEntity,
        GridComponent grid,
        int2 cell)
    {
        if (!IsInsideGrid(cell, grid))
            return -1;

        if (!entityManager.HasBuffer<GridNodeCost>(gridEntity))
            return -2;

        DynamicBuffer<GridNodeCost> costs =
            entityManager.GetBuffer<GridNodeCost>(gridEntity);

        int index = GridHelper.GetNodeIndex(cell, grid);

        if (index < 0 || index >= costs.Length)
            return -3;

        return costs[index].cost;
    }

    private static bool IsInsideGrid(int2 cell, GridComponent grid)
    {
        return cell.x >= 0 &&
               cell.x < grid.width &&
               cell.y >= 0 &&
               cell.y < grid.height;
    }

    private static string Format(float3 value)
    {
        return $"({value.x:F2}, {value.y:F2}, {value.z:F2})";
    }

    private static string Format(int2 value)
    {
        return $"({value.x}, {value.y})";
    }
}

/// <summary>
/// Debug-only tracker. It observes newly spawned enemies for a few seconds and
/// explains where the movement pipeline stops: target assignment, path request,
/// flow-field assignment, field calculation or velocity generation.
/// </summary>
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(MovementAgentActuatorSystem))]
public partial struct EnemySpawnMoveDebugSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (debug, transform, move, entity) in
                 SystemAPI.Query<
                         RefRW<EnemySpawnMoveDebug>,
                         RefRO<LocalTransform>,
                         RefRO<MovementAgentComponent>>()
                     .WithEntityAccess())
        {
            debug.ValueRW.elapsed += deltaTime;

            if (debug.ValueRO.elapsed + 0.0001f < debug.ValueRO.nextLogTime)
                continue;

            debug.ValueRW.nextLogTime += debug.ValueRO.logInterval;

            bool hasRequest =
                entityManager.HasComponent<TargetChangeRequest>(entity);

            bool hasSteering =
                entityManager.HasComponent<MovementSteeringComponent>(entity);

            bool hasDefaultSetup =
                entityManager.HasComponent<SetupUnitMoverDefaultPosition>(entity);

            bool hasMoveOverride =
                entityManager.HasComponent<MoveOverride>(entity);

            bool moveOverrideEnabled =
                hasMoveOverride && entityManager.IsComponentEnabled<MoveOverride>(entity);

            string steeringInfo = "Missing";

            if (hasSteering)
            {
                MovementSteeringComponent steering =
                    entityManager.GetComponentData<MovementSteeringComponent>(entity);

                steeringInfo =
                    $"IsSettled={steering.isSettled}, StuckTime={steering.stuckTime:F2}, " +
                    $"StoppingDistance={steering.stoppingDistance:F2}";
            }

            string fieldInfo = BuildFieldInfo(entityManager, move.ValueRO.FieldEntity);

            float movedDistance = math.distance(
                transform.ValueRO.Position,
                debug.ValueRO.spawnPosition);

            float remainingDistance = math.distance(
                transform.ValueRO.Position,
                debug.ValueRO.expectedTarget);

            Debug.Log(
                $"[EnemyMoveDebug] Enemy={entity}, T={debug.ValueRO.elapsed:F2}s, " +
                $"Position={Format(transform.ValueRO.Position)}, Moved={movedDistance:F2}, " +
                $"HasTarget={move.ValueRO.hastarget}, CurrentWorldTarget={Format(move.ValueRO.currentworldtarget)}, " +
                $"ExpectedTarget={Format(debug.ValueRO.expectedTarget)}, Remaining={remainingDistance:F2}, " +
                $"Velocity={Format(move.ValueRO.velocity)}, PreferredVelocity={Format(move.ValueRO.preferredVelocity)}, " +
                $"Field={fieldInfo}, HasTargetChangeRequest={hasRequest}, " +
                $"HasSetupUnitMoverDefaultPosition={hasDefaultSetup}, " +
                $"MoveOverrideEnabled={moveOverrideEnabled}, Steering=[{steeringInfo}].");

            if (!move.ValueRO.hastarget &&
                remainingDistance > 1f &&
                !debug.ValueRO.warnedTargetLost)
            {
                debug.ValueRW.warnedTargetLost = true;

                Debug.LogWarning(
                    $"[EnemyMoveDebug][TargetLost] Enemy={entity} no longer has a movement target " +
                    $"but is still {remainingDistance:F2} units away. Read the preceding debug line: " +
                    "if StuckTime reached its threshold, MovementAgentActuatorSystem settled the unit; " +
                    "if HasSetupUnitMoverDefaultPosition=True, a setup component is overwriting the target; " +
                    "if FieldEntity stays null, inspect the flow-field request pipeline.");
            }

            if (debug.ValueRO.elapsed >= debug.ValueRO.duration)
            {
                ecb.RemoveComponent<EnemySpawnMoveDebug>(entity);
            }
        }

        ecb.Playback(entityManager);
        ecb.Dispose();
    }

    private static string BuildFieldInfo(
        EntityManager entityManager,
        Entity fieldEntity)
    {
        if (fieldEntity == Entity.Null)
            return "Entity.Null";

        if (!entityManager.Exists(fieldEntity))
            return $"{fieldEntity} (MissingEntity)";

        string result = fieldEntity.ToString();

        if (entityManager.HasComponent<FlowField>(fieldEntity))
        {
            FlowField flowField = entityManager.GetComponentData<FlowField>(fieldEntity);
            result += $", TargetCell=({flowField.targetcell.x}, {flowField.targetcell.y})";
        }

        if (entityManager.HasComponent<FlowFieldStatus>(fieldEntity))
        {
            FlowFieldStatus status = entityManager.GetComponentData<FlowFieldStatus>(fieldEntity);
            result += $", Status={status.Value}";
        }
        else
        {
            result += ", Status=Missing";
        }

        return result;
    }

    private static string Format(float3 value)
    {
        return $"({value.x:F2}, {value.y:F2}, {value.z:F2})";
    }
}
