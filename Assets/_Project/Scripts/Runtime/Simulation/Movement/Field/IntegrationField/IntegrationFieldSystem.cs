using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Unity.Profiling;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(GridIslandSystem))]
[UpdateAfter(typeof(GridNeighborMaskSystem))]
public partial struct IntegrationFieldSystem : ISystem
{
    private EntityQuery m_FieldQuery;
    public const int MaxIslands = 10000;
    private static readonly ProfilerMarker ResetMarker = new ProfilerMarker("Integration.Reset");
    private static readonly ProfilerMarker SeedMarker = new ProfilerMarker("Integration.Seeds");
    private static readonly ProfilerMarker PropagationMarker = new ProfilerMarker("Integration.Propagate");
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        // Chỉ cần query cho các FlowField
        m_FieldQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAllRW<FlowFieldStatus>()
            .WithAllRW<FieldNode>()
            .WithAll<FlowField>()
            .Build(ref state);

        state.RequireForUpdate<GridComponent>();
        state.RequireForUpdate(m_FieldQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<GridComponent>())
            return;

        var gridEntity = SystemAPI.GetSingletonEntity<GridComponent>();
        var grid = SystemAPI.GetSingleton<GridComponent>();
        if (!SystemAPI.HasComponent<GridNeighborMaskState>(gridEntity)) return;
        var maskState = SystemAPI.GetComponent<GridNeighborMaskState>(gridEntity);
        if (!maskState.Built || maskState.TopologyVersion != grid.topologyVersion
            || maskState.Width != grid.width || maskState.Height != grid.height) return;
        
        var gridCosts = SystemAPI.GetBufferLookup<GridNodeCost>(true)[gridEntity].AsNativeArray();
        var gridIslands = SystemAPI.GetBufferLookup<GridIsland>(true)[gridEntity].AsNativeArray();
        if (!SystemAPI.HasBuffer<GridNeighborMask>(gridEntity)) return;
        var neighborMasks = SystemAPI.GetBufferLookup<GridNeighborMask>(true)[gridEntity].AsNativeArray();
        if (neighborMasks.Length != grid.width * grid.height) return;

        // Neighbor offsets and integer edge costs for bucket Dijkstra.
        NativeArray<int2> nativeNeighbors = new NativeArray<int2>(8, Allocator.TempJob);
        nativeNeighbors[0] = new int2(0, 1);
        nativeNeighbors[1] = new int2(1, 0);
        nativeNeighbors[2] = new int2(0, -1);
        nativeNeighbors[3] = new int2(-1, 0);
        nativeNeighbors[4] = new int2(1, -1);
        nativeNeighbors[5] = new int2(-1, 1);
        nativeNeighbors[6] = new int2(-1, -1);
        nativeNeighbors[7] = new int2(1, 1);

        NativeArray<int> nativeCosts = new NativeArray<int>(8, Allocator.TempJob);
        nativeCosts[0] = 10; nativeCosts[1] = 10; nativeCosts[2] = 10; nativeCosts[3] = 10;
        nativeCosts[4] = 14; nativeCosts[5] = 14; nativeCosts[6] = 14; nativeCosts[7] = 14;

        var blockageQuery = SystemAPI.QueryBuilder().WithAll<BlockageData, LocalTransform>().Build();
        var blockageDatas = blockageQuery.ToComponentDataArray<BlockageData>(Allocator.TempJob);
        var blockageLocalToWorlds = BlockageTransforms.Capture(blockageQuery, Allocator.TempJob);

        var job = new CalculateIntegrationFieldJob
        {
            Grid = grid,
            GridCosts = gridCosts,
            GridIslands = gridIslands,
            NeighborMasks = neighborMasks,
            BlockageDatas = blockageDatas,
            BlockageLocalToWorlds = blockageLocalToWorlds,
            NeighborsDir = nativeNeighbors,
            DirCost = nativeCosts,
            FlowFieldType = SystemAPI.GetComponentTypeHandle<FlowField>(true),
            StatusType = SystemAPI.GetComponentTypeHandle<FlowFieldStatus>(false),
            NodeType = SystemAPI.GetBufferTypeHandle<FieldNode>(false),
            IslandSeedType = SystemAPI.GetBufferTypeHandle<IslandSeed>(false)
        };

        state.Dependency = job.ScheduleParallel(m_FieldQuery, state.Dependency);
        
        nativeNeighbors.Dispose(state.Dependency);
        nativeCosts.Dispose(state.Dependency);
        blockageDatas.Dispose(state.Dependency);
        blockageLocalToWorlds.Dispose(state.Dependency);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state) { }

    [BurstCompile]
    public struct CalculateIntegrationFieldJob : IJobChunk
    {
        [ReadOnly] public GridComponent Grid;
        [ReadOnly] public NativeArray<GridNodeCost> GridCosts;
        [ReadOnly] public NativeArray<GridIsland> GridIslands;
        [ReadOnly] public NativeArray<GridNeighborMask> NeighborMasks;
        [ReadOnly] public NativeArray<BlockageData> BlockageDatas;
        [ReadOnly] public NativeArray<LocalToWorld> BlockageLocalToWorlds;
        [ReadOnly] public NativeArray<int2> NeighborsDir;
        [ReadOnly] public NativeArray<int> DirCost;

        [ReadOnly] public ComponentTypeHandle<FlowField> FlowFieldType;
        public ComponentTypeHandle<FlowFieldStatus> StatusType;
        public BufferTypeHandle<FieldNode> NodeType;
        public BufferTypeHandle<IslandSeed> IslandSeedType;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
    {
        NativeArray<FlowField> flowFields = chunk.GetNativeArray(ref FlowFieldType);
        NativeArray<FlowFieldStatus> statuses = chunk.GetNativeArray(ref StatusType);
        BufferAccessor<FieldNode> nodeBuffers = chunk.GetBufferAccessor(ref NodeType);
        BufferAccessor<IslandSeed> seedBuffers = chunk.GetBufferAccessor(ref IslandSeedType);

        for (int e = 0; e < chunk.Count; e++)
        {
            if (statuses[e].Value != FieldState.Requested && statuses[e].Value != FieldState.PendingRecalculation)
                continue;

            FlowFieldStatus status = statuses[e];
            status.Value = FieldState.CalculatingCost;
            statuses[e] = status;

            FlowField field = flowFields[e];
            DynamicBuffer<FieldNode> nbuffer = nodeBuffers[e];

            if (nbuffer.Length == 0) continue;

            // KIỂM TRA TARGET HỢP LỆ
            bool isTargetInBounds = field.targetcell.x >= 0 && field.targetcell.x < Grid.width &&
                                   field.targetcell.y >= 0 && field.targetcell.y < Grid.height;

            if (!isTargetInBounds)
            {
                status.Value = FieldState.CalculatingDirection;
                statuses[e] = status;
                continue;
            }

            // 1. Reset all nodes
            ResetMarker.Begin();
            for (int i = 0; i < nbuffer.Length; i++)
            {
                FieldNode node = nbuffer[i];
                node.bestcost = int.MaxValue;
                nbuffer[i] = node;
            }

            ResetMarker.End();
            SeedMarker.Begin();
            IntegrationBucketQueue integrationQueue = new IntegrationBucketQueue(Allocator.Temp);

            NativeArray<int> bestSeedPerIsland = new NativeArray<int>(IntegrationFieldSystem.MaxIslands, Allocator.Temp); 
            NativeArray<float> minDistancePerIsland = new NativeArray<float>(IntegrationFieldSystem.MaxIslands, Allocator.Temp);
            
            for (int i = 0; i < 1000; i++)
            {
                bestSeedPerIsland[i] = -1;
                minDistancePerIsland[i] = float.MaxValue;
            }

            float2 targetPos2D = new float2(field.targetcell.x, field.targetcell.y);
            
            if (IntegrationSeedUtility.TrySingleIslandTarget(field.targetcell.x, field.targetcell.y,
                Grid.width, Grid.height, Grid.islandCount, Grid.islandTopologyVersion, Grid.topologyVersion,
                GridCosts, GridIslands, out int directSeed, out int directIsland))
            {
                bestSeedPerIsland[directIsland] = directSeed;
                minDistancePerIsland[directIsland] = 0;
            }
            else for (int i = 0; i < nbuffer.Length; i++)
            {
                int islandID = GridIslands[i].islandID;
                if (islandID <= 0 || islandID >= IntegrationFieldSystem.MaxIslands) continue;
                if (GridCosts[i].cost >= 255 || GridCosts[i].cost == int.MaxValue) continue;

                int2 posInt = GridHelper.GetGridPosFromIndex(i, Grid);
                float2 pos2D = new float2(posInt.x, posInt.y);
                float distSq = math.distancesq(pos2D, targetPos2D);

                if (distSq < minDistancePerIsland[islandID])
                {
                    minDistancePerIsland[islandID] = distSq;
                    bestSeedPerIsland[islandID] = i;
                }
            }

            DynamicBuffer<IslandSeed> sbuffer = seedBuffers[e];
            sbuffer.Clear();

            // 1. Kiểm tra xem điểm click (targetcell) có nằm trong Blockage nào không
            bool foundTargetBlockage = NaturalBlockedTargetResolver.TryGetBlockageGridBounds(
                field.targetcell,
                Grid,
                BlockageDatas,
                BlockageLocalToWorlds,
                out int2 bMinGrid,
                out int2 bMaxGrid);

            // 2. Nạp IslandSeed đại diện cho từng đảo vào sbuffer (dùng khi di chuyển khác đảo)
            for (int i = 1; i < 1000; i++)
            {
                int seedIndex = bestSeedPerIsland[i];
                if (seedIndex != -1)
                {
                    float3 worldPos = GridHelper.GridToWorld(GridHelper.GetGridPosFromIndex(seedIndex, Grid), Grid);
                    sbuffer.Add(new IslandSeed { islandID = i, seedPosition = worldPos });
                }
            }

            // 3. All seeds enter the cost-zero bucket.
            if (foundTargetBlockage)
            {
                // NẾU CLICK VÀO BLOCKAGE: Tạo Đa Seed trên TOÀN BỘ 4 CẠNH VIỀN NGOÀI (Chỉ lấy ô đất trống Cost < 255)
                int startX = math.max(0, bMinGrid.x - 1);
                int endX = math.min(Grid.width - 1, bMaxGrid.x + 1);
                int startY = math.max(0, bMinGrid.y - 1);
                int endY = math.min(Grid.height - 1, bMaxGrid.y + 1);

                for (int x = startX; x <= endX; x++)
                {
                    for (int y = startY; y <= endY; y++)
                    {
                        if (x == startX || x == endX || y == startY || y == endY)
                        {
                            int pIndex = GridHelper.GetNodeIndex(new int2(x, y), Grid);
                            if (GridCosts[pIndex].cost < 255 && GridCosts[pIndex].cost != int.MaxValue && GridIslands[pIndex].islandID > 0)
                            {
                                if (nbuffer[pIndex].bestcost != 0)
                                {
                                    FieldNode node = nbuffer[pIndex];
                                    node.bestcost = 0;
                                    nbuffer[pIndex] = node;
                                    integrationQueue.Enqueue(pIndex, 0);
                                }
                            }
                        }
                    }
                }

                // Với các đảo khác (bị cô lập), nạp thêm seed của đảo đó
                for (int i = 1; i < 1000; i++)
                {
                    int seedIndex = bestSeedPerIsland[i];
                    if (seedIndex != -1 && nbuffer[seedIndex].bestcost != 0)
                    {
                        FieldNode node = nbuffer[seedIndex];
                        node.bestcost = 0;
                        nbuffer[seedIndex] = node;
                        integrationQueue.Enqueue(seedIndex, 0);
                    }
                }
            }
            else
            {
                // NẾU CLICK ĐẤT TRỐNG: Nạp seed đơn bình thường
                for (int i = 1; i < 1000; i++)
                {
                    int seedIndex = bestSeedPerIsland[i];
                    if (seedIndex != -1)
                    {
                        if (nbuffer[seedIndex].bestcost != 0)
                        {
                            FieldNode node = nbuffer[seedIndex];
                            node.bestcost = 0;
                            nbuffer[seedIndex] = node;
                            integrationQueue.Enqueue(seedIndex, 0);
                        }
                    }
                }
            }

            bestSeedPerIsland.Dispose();
            minDistancePerIsland.Dispose();
            SeedMarker.End();

            // 4. Process lowest cost first; an improved node leaves a stale entry behind.
            PropagationMarker.Begin();
            while (integrationQueue.TryDequeue(out int current, out int queuedCost))
            {
                int currentbestcost = nbuffer[current].bestcost;
                if (queuedCost != currentbestcost)
                    continue;
                uint directions = NeighborMasks[current].Value;
                while (directions != 0)
                {
                    int i = math.tzcnt(directions);
                    directions &= directions - 1;
                    int neighborsindex = current + NeighborsDir[i].x + NeighborsDir[i].y * Grid.width;
                    int neighborcost = GridCosts[neighborsindex].cost;

                    int newcost = neighborcost + currentbestcost + DirCost[i];
                    
                    if (newcost < nbuffer[neighborsindex].bestcost)
                    {
                        FieldNode node = nbuffer[neighborsindex];
                        node.bestcost = newcost;
                        nbuffer[neighborsindex] = node;
                        integrationQueue.Enqueue(neighborsindex, newcost);
                    }
                }
            }
            integrationQueue.Dispose();
            PropagationMarker.End();

            status.Value = FieldState.CalculatingDirection;
            statuses[e] = status;
        }
    }
}
}
