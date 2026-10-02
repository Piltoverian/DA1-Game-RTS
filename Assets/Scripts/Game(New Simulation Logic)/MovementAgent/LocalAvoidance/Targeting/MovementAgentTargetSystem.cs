using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(FlowDirectionSystem))]
public partial struct MovementAgentTargetSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var grid = SystemAPI.GetSingleton<GridComponent>();
        var gridEntity = SystemAPI.GetSingletonEntity<GridComponent>();
        var deltaTime = SystemAPI.Time.DeltaTime;

        var blockageQuery = SystemAPI.QueryBuilder().WithAll<BlockageData, LocalToWorld>().Build();
        var blockageDatas = blockageQuery.ToComponentDataArray<BlockageData>(Allocator.TempJob);
        var blockageLocalToWorlds = blockageQuery.ToComponentDataArray<LocalToWorld>(Allocator.TempJob);

        var job = new UnitTargetJob
        {
            Grid = grid,
            FieldNodeLookup = SystemAPI.GetBufferLookup<FieldNode>(true),
            IslandSeedLookup = SystemAPI.GetBufferLookup<IslandSeed>(true),
            GridIslands = SystemAPI.GetBuffer<GridIsland>(gridEntity).AsNativeArray(),
            GridCosts = SystemAPI.GetBuffer<GridNodeCost>(gridEntity).AsNativeArray(),
            BlockageDatas = blockageDatas,
            BlockageLocalToWorlds = blockageLocalToWorlds,
            DeltaTime = deltaTime
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
        blockageDatas.Dispose(state.Dependency);
        blockageLocalToWorlds.Dispose(state.Dependency);
    }

    [BurstCompile]
    public partial struct UnitTargetJob : IJobEntity
    {
        [ReadOnly] public GridComponent Grid;
        [ReadOnly] public BufferLookup<FieldNode> FieldNodeLookup;
        [ReadOnly] public BufferLookup<IslandSeed> IslandSeedLookup;
        [ReadOnly] public NativeArray<GridIsland> GridIslands;
        [ReadOnly] public NativeArray<GridNodeCost> GridCosts;
        [ReadOnly] public NativeArray<BlockageData> BlockageDatas;
        [ReadOnly] public NativeArray<LocalToWorld> BlockageLocalToWorlds;
        [ReadOnly] public float DeltaTime;

        public void Execute(Entity entity, [ReadOnly] in LocalTransform transform, 
            ref MovementAgentComponent move, 
            ref MovementSteeringComponent steering)
        {
            float3 pos = transform.Position;
            float3 globalTarget = move.currentworldtarget;
            float distToGlobal = math.distance(pos, globalTarget);
            float3 islandGoal = globalTarget;

            move.preferredVelocity = float3.zero;

            if (!move.hastarget || move.targetResolutionKind == TargetResolutionKind.None)
            {
                steering.stuckTime = 0;
                steering.minDistanceToTarget = float.MaxValue;
                return;
            }

            if (move.FieldEntity == Entity.Null || !FieldNodeLookup.HasBuffer(move.FieldEntity)) return;
            var buffer = FieldNodeLookup[move.FieldEntity];

            int2 gridPos = GridHelper.WorldToGrid(pos, Grid);
            int nodeIndex = GridHelper.GetNodeIndex(gridPos, Grid);
            int unitIsland = GridIslands[nodeIndex].islandID;

            // --- 1. ISLAND SYNC & BLOCKAGE FOOTPRINT TARGETING ---
            int2 targetGrid = GridHelper.WorldToGrid(globalTarget, Grid);
            bool foundBuilding = NaturalBlockedTargetResolver.TryGetBlockageGridBounds(
                targetGrid,
                Grid,
                BlockageDatas,
                BlockageLocalToWorlds,
                out int2 bMinGrid,
                out int2 bMaxGrid);

            if (foundBuilding)
            {
                int startX = math.max(0, bMinGrid.x - 1);
                int endX = math.min(Grid.width - 1, bMaxGrid.x + 1);
                int startY = math.max(0, bMinGrid.y - 1);
                int endY = math.min(Grid.height - 1, bMaxGrid.y + 1);

                float minDistSq = float.MaxValue;
                float3 bestSlot = globalTarget;
                bool foundSlot = false;

                for (int x = startX; x <= endX; x++)
                {
                    for (int y = startY; y <= endY; y++)
                    {
                        if (x == startX || x == endX || y == startY || y == endY)
                        {
                            int pIndex = GridHelper.GetNodeIndex(new int2(x, y), Grid);
                            if (GridCosts[pIndex].cost < 255 && GridCosts[pIndex].cost != int.MaxValue && GridIslands[pIndex].islandID == unitIsland)
                            {
                                float3 slotWorldPos = GridHelper.GridToWorld(new int2(x, y), Grid);
                                float dSq = math.distancesq(pos, slotWorldPos);
                                if (dSq < minDistSq)
                                {
                                    minDistSq = dSq;
                                    bestSlot = slotWorldPos;
                                    foundSlot = true;
                                }
                            }
                        }
                    }
                }

                if (foundSlot)
                {
                    islandGoal = bestSlot;
                }
            }
            else
            {
                if (!NaturalBlockedTargetResolver.IsCellInBounds(move.navigationTargetCell, Grid))
                {
                    return;
                }

                float3 baseGoal = move.targetResolutionKind == TargetResolutionKind.NaturalResolved
                    ? GridHelper.GridToWorld(move.navigationTargetCell, Grid)
                    : globalTarget;

                islandGoal = baseGoal;

                int resolvedIndex = GridHelper.GetNodeIndex(move.navigationTargetCell, Grid);
                int targetIsland = (resolvedIndex >= 0 && resolvedIndex < GridIslands.Length) ? GridIslands[resolvedIndex].islandID : 0;

                if (targetIsland > 0 && unitIsland != targetIsland && IslandSeedLookup.HasBuffer(move.FieldEntity))
                {
                    var seedBuffer = IslandSeedLookup[move.FieldEntity];
                    for (int i = 0; i < seedBuffer.Length; i++)
                    {
                        if (seedBuffer[i].islandID == unitIsland)
                        {
                            islandGoal = seedBuffer[i].seedPosition;
                            break;
                        }
                    }
                }
            }

            move.realTarget = islandGoal;
            float3 finalGoal = move.realTarget;
            float distToFinal = math.distance(pos, finalGoal);

            // --- 2. ARRIVAL CHECK ---
            if (distToFinal < steering.stoppingDistance)
            {
                move.hastarget = false;
                steering.isSettled = true;
                return;
            }

            // --- 3. CALCULATE DESIRED VELOCITY ---
            float3 flowVelocity = UnitMovementMath.CalculateFlowVelocity(
                pos, buffer.AsNativeArray(), Grid.origin, Grid.cellsize, Grid.width, Grid.height, move.speed
            );

            float3 directDir = finalGoal - pos;
            directDir.y = 0;
            float distToFinalGoal = math.length(directDir);
            float3 directVelocity = distToFinalGoal > 0.001f ? (directDir / distToFinalGoal) * move.speed : float3.zero;

            // Chuyển mượt từ FlowField sang Direct Aim khi vào gần đích
            float blendRange = math.max(steering.arrivalRadius * 1.5f, Grid.cellsize * 2f);
            float targetWeight = math.clamp(1.0f - (distToFinalGoal / blendRange), 0f, 1f);
            if (distToFinalGoal < Grid.cellsize * 2f) targetWeight = math.max(targetWeight, 0.5f);

            move.preferredVelocity = math.lerp(flowVelocity, directVelocity, targetWeight);

            // --- 4. ARRIVAL DAMPING (Hãm phanh mượt khi tới gần) ---
            if (distToFinalGoal < steering.arrivalRadius)
            {
                float speedMultiplier = math.clamp(distToFinalGoal / steering.arrivalRadius, 0.1f, 1.0f);
                move.preferredVelocity *= speedMultiplier;
            }

            // --- 5. PROGRESS TRACKING ---
            if (steering.minDistanceToTarget <= 0) steering.minDistanceToTarget = float.MaxValue;
            if (distToFinal < steering.minDistanceToTarget - DeltaTime * move.speed * 0.6f)
            {
                steering.minDistanceToTarget = distToFinal;
                steering.stuckTime = 0; 
            }
        }
    }
}
