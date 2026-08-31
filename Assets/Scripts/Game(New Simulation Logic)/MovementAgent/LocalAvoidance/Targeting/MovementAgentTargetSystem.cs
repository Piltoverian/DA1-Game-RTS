using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(FlowDirectionSystem))]
[UpdateAfter(typeof(MovementAgentGroupFormationSystem))]
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

            // Mặc định preferred velocity = zero (ORCA sẽ dùng giá trị này)
            move.preferredVelocity = float3.zero;

            if (!move.hastarget)
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
            float3 targetWorldPos = globalTarget;
            float2 targetWorld2D = new float2(targetWorldPos.x, targetWorldPos.z);
            int2 targetGrid = GridHelper.WorldToGrid(globalTarget, Grid);
            bool foundBuilding = false;
            int2 bMinGrid = targetGrid;
            int2 bMaxGrid = targetGrid;

            for (int b = 0; b < BlockageDatas.Length; b++)
            {
                BlockageData bData = BlockageDatas[b];
                float3 bPos = BlockageLocalToWorlds[b].Position;

                float2 worldMin = new float2(bPos.x + bData.LocalRect.MinPoint.x, bPos.z + bData.LocalRect.MinPoint.y);
                float2 worldMax = new float2(bPos.x + bData.LocalRect.MaxPoint.x, bPos.z + bData.LocalRect.MaxPoint.y);

                StartEndRect worldRect = new StartEndRect(worldMin);
                worldRect.ExpandTo(worldMax);

                if (worldRect.isContains(targetWorld2D))
                {
                    float3 minWorld3D = new float3(worldRect.MinPoint.x + 0.01f, 0, worldRect.MinPoint.y + 0.01f);
                    float3 maxWorld3D = new float3(worldRect.MaxPoint.x - 0.01f, 0, worldRect.MaxPoint.y - 0.01f);

                    int2 bMin = GridHelper.WorldToGrid(minWorld3D, Grid);
                    int2 bMax = GridHelper.WorldToGrid(maxWorld3D, Grid);

                    bMinGrid = math.min(bMin, bMax);
                    bMaxGrid = math.max(bMin, bMax);
                    foundBuilding = true;
                    break;
                }
            }

            if (foundBuilding)
            {
                // Quét đúng các ô đất trống viền ngoài của công trình này và chọn ô gần vị trí lính nhất
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
                int targetIndex = GridHelper.GetNodeIndex(targetGrid, Grid);
                bool isTargetInGrid = targetGrid.x >= 0 && targetGrid.x < Grid.width && targetGrid.y >= 0 && targetGrid.y < Grid.height;
                bool isTargetBlocked = isTargetInGrid && (GridCosts[targetIndex].cost >= 255 || GridCosts[targetIndex].cost == int.MaxValue);

                if (isTargetBlocked)
                {
                    // LƯỚI AN TOÀN: Ô click là vật cản Cost >= 255 -> Tự động tìm ô đất trống gần nhất thay vì đi vào trong!
                    float minDistSq = float.MaxValue;
                    float3 nearestWalkable = globalTarget;
                    bool foundWalkable = false;

                    for (int r = 1; r <= 8 && !foundWalkable; r++)
                    {
                        for (int dx = -r; dx <= r; dx++)
                        {
                            for (int dy = -r; dy <= r; dy++)
                            {
                                if (math.abs(dx) == r || math.abs(dy) == r)
                                {
                                    int2 neighbor = targetGrid + new int2(dx, dy);
                                    if (neighbor.x >= 0 && neighbor.x < Grid.width && neighbor.y >= 0 && neighbor.y < Grid.height)
                                    {
                                        int nIdx = GridHelper.GetNodeIndex(neighbor, Grid);
                                        if (GridCosts[nIdx].cost < 255 && GridCosts[nIdx].cost != int.MaxValue && GridIslands[nIdx].islandID == unitIsland)
                                        {
                                            float3 nPos = GridHelper.GridToWorld(neighbor, Grid);
                                            float dSq = math.distancesq(pos, nPos);
                                            if (dSq < minDistSq)
                                            {
                                                minDistSq = dSq;
                                                nearestWalkable = nPos;
                                                foundWalkable = true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    islandGoal = foundWalkable ? nearestWalkable : globalTarget;
                }
                else
                {
                    int targetIsland = (isTargetInGrid && targetIndex < GridIslands.Length) ? GridIslands[targetIndex].islandID : 0;

                    // Chỉ khi lính ở KHÁC đảo với mục tiêu (bị ngăn cách) mới dùng IslandSeed của đảo đó để ra bờ mép
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
                    else
                    {
                        islandGoal = globalTarget;
                    }
                }
            }

            move.realTarget = islandGoal;
            float3 finalGoal = move.realTarget;

            // --- 2. SLOT FORMATION ---
            if (math.lengthsq(move.slotTarget) > 0.001f && distToGlobal < steering.formationRange)
            {
                // Kiểm tra xem slot còn hợp lệ không (có thể địa hình vừa bị thay đổi như xây nhà)
                int2 slotCell = GridHelper.WorldToGrid(move.slotTarget, Grid);
                bool isSlotValid = true;
                if (slotCell.x >= 0 && slotCell.x < Grid.width && slotCell.y >= 0 && slotCell.y < Grid.height)
                {
                    int slotNodeIndex = GridHelper.GetNodeIndex(slotCell, Grid);
                    if (GridCosts[slotNodeIndex].cost >= 255 || GridCosts[slotNodeIndex].cost == int.MaxValue) isSlotValid = false;
                }
                else isSlotValid = false;

                if (!isSlotValid)
                {
                    move.slotTarget = float3.zero;
                    move.useSlotTarget = false;
                }
                else
                {
                    int currentCellcost = buffer[nodeIndex].bestcost;
                    float directDistToSlot = math.distance(pos, move.slotTarget);

                    float2 flowDir = buffer[nodeIndex].direction;
                    float2 toSlotDir = math.normalizesafe(new float2(
                        move.slotTarget.x - pos.x,
                        move.slotTarget.z - pos.z
                    ));
                    float dotFlowSlot = math.dot(flowDir, toSlotDir);

                    bool slotAlongPath = dotFlowSlot > -0.2f || directDistToSlot < steering.stoppingDistance * 3f;

                    if (slotAlongPath)
                    {
                        float pathDist = currentCellcost / 10f;
                        if (currentCellcost == int.MaxValue || pathDist <= directDistToSlot * 1.8f)
                        {
                            finalGoal = move.slotTarget;
                            if (distToGlobal < steering.arrivalRadius) move.useSlotTarget = true;
                        }
                    }
                }
            }

            float distToFinal = math.distance(pos, finalGoal);

            // --- 3. ARRIVAL CHECK ---
            if (distToFinal < steering.stoppingDistance)
            {
                move.hastarget = false;
                steering.isSettled = true;
                return;
            }

            // --- 4. CALCULATE DESIRED VELOCITY ---
            float3 flowVelocity = UnitMovementMath.CalculateFlowVelocity(
                pos, buffer.AsNativeArray(), Grid.origin, Grid.cellsize, Grid.width, Grid.height, move.speed
            );

            float3 directDir = finalGoal - pos;
            directDir.y = 0;
            float distToFinalGoal = math.length(directDir);
            float3 directVelocity = distToFinalGoal > 0.001f ? (directDir / distToFinalGoal) * move.speed : float3.zero;

            float targetWeight = math.clamp(1.0f - (distToFinalGoal / steering.formationRange), 0f, 1f);
            if (distToFinalGoal < Grid.cellsize * 2f) targetWeight = math.max(targetWeight, 0.5f);

            move.preferredVelocity = math.lerp(flowVelocity, directVelocity, targetWeight);

            // --- 5. ARRIVAL DAMPING ---
            if (distToFinalGoal < steering.arrivalRadius)
            {
                float speedMultiplier = math.clamp(distToFinalGoal / steering.arrivalRadius, 0.1f, 1.0f);
                move.preferredVelocity *= speedMultiplier;
            }

            // --- 6. ANTI-DEADLOCK TRACKING ---
            //stuckscale theo speed
            if (steering.minDistanceToTarget <= 0) steering.minDistanceToTarget = float.MaxValue;
            if (distToFinal < steering.minDistanceToTarget - DeltaTime*move.speed*0.6f)
            {
                steering.minDistanceToTarget = distToFinal;
                steering.stuckTime = 0; 
            }
            // Chú ý: stuckTime sẽ được tăng lên trong ActuatorSystem dựa trên DeltaTime
        }
    }
}
