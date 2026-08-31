using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(GridIslandSystem))]
public partial struct IntegrationFieldSystem : ISystem
{
    private EntityQuery m_FieldQuery;
    public const int MaxIslands = 10000;
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
        
        var gridCosts = SystemAPI.GetBuffer<GridNodeCost>(gridEntity).AsNativeArray();
        var gridIslands = SystemAPI.GetBuffer<GridIsland>(gridEntity).AsNativeArray();

        // Neighbors và Costs cho BFS
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

        var blockageQuery = SystemAPI.QueryBuilder().WithAll<BlockageData, LocalToWorld>().Build();
        var blockageDatas = blockageQuery.ToComponentDataArray<BlockageData>(Allocator.TempJob);
        var blockageLocalToWorlds = blockageQuery.ToComponentDataArray<LocalToWorld>(Allocator.TempJob);

        var job = new CalculateIntegrationFieldJob
        {
            Grid = grid,
            GridCosts = gridCosts,
            GridIslands = gridIslands,
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
            for (int i = 0; i < nbuffer.Length; i++)
            {
                FieldNode node = nbuffer[i];
                node.bestcost = int.MaxValue;
                nbuffer[i] = node;
            }

            NativeQueue<int> queryforBFS = new NativeQueue<int>(Allocator.Temp);

            NativeArray<int> bestSeedPerIsland = new NativeArray<int>(IntegrationFieldSystem.MaxIslands, Allocator.Temp); 
            NativeArray<float> minDistancePerIsland = new NativeArray<float>(IntegrationFieldSystem.MaxIslands, Allocator.Temp);
            
            for (int i = 0; i < 1000; i++)
            {
                bestSeedPerIsland[i] = -1;
                minDistancePerIsland[i] = float.MaxValue;
            }

            float2 targetPos2D = new float2(field.targetcell.x, field.targetcell.y);
            
            for (int i = 0; i < nbuffer.Length; i++)
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
            float3 targetWorldPos = GridHelper.GridToWorld(field.targetcell, Grid);
            float2 targetWorld2D = new float2(targetWorldPos.x, targetWorldPos.z);

            bool foundTargetBlockage = false;
            int2 bMinGrid = field.targetcell;
            int2 bMaxGrid = field.targetcell;

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
                    foundTargetBlockage = true;
                    break;
                }
            }

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

            // 3. Nạp Seed vào BFS
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
                                    queryforBFS.Enqueue(pIndex);
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
                        queryforBFS.Enqueue(seedIndex);
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
                            queryforBFS.Enqueue(seedIndex);
                        }
                    }
                }
            }

            bestSeedPerIsland.Dispose();
            minDistancePerIsland.Dispose();

            // 4. BFS
            while (queryforBFS.TryDequeue(out int current))
            {
                int2 gridPos = GridHelper.GetGridPosFromIndex(current, Grid);
                int currentbestcost = nbuffer[current].bestcost;

                for (int i = 0; i < 8; i++)
                {
                    int2 neighborsgridpos = gridPos + NeighborsDir[i];
                    
                    if (neighborsgridpos.x < 0 || neighborsgridpos.x >= Grid.width || 
                        neighborsgridpos.y < 0 || neighborsgridpos.y >= Grid.height)
                        continue;

                    if (i > 3)
                    {
                        int2 orthox = gridPos + new int2(NeighborsDir[i].x, 0);
                        int2 orthoy = gridPos + new int2(0, NeighborsDir[i].y);
                        
                        if (orthox.x < 0 || orthox.x >= Grid.width || orthox.y < 0 || orthox.y >= Grid.height ||
                            orthoy.x < 0 || orthoy.x >= Grid.width || orthoy.y < 0 || orthoy.y >= Grid.height)
                            continue;

                        int orthoxindex = GridHelper.GetNodeIndex(orthox, Grid);
                        int orthoyindex = GridHelper.GetNodeIndex(orthoy, Grid);
                        
                        if (GridCosts[orthoxindex].cost >= 255 || GridCosts[orthoyindex].cost >= 255 || 
                            GridCosts[orthoxindex].cost == int.MaxValue || GridCosts[orthoyindex].cost == int.MaxValue)
                            continue;
                    }

                    int neighborsindex = GridHelper.GetNodeIndex(neighborsgridpos, Grid);
                    int neighborcost = GridCosts[neighborsindex].cost;

                    if (neighborcost >= 255 || neighborcost == int.MaxValue)
                        continue;

                    int newcost = neighborcost + currentbestcost + DirCost[i];
                    
                    if (newcost < nbuffer[neighborsindex].bestcost)
                    {
                        FieldNode node = nbuffer[neighborsindex];
                        node.bestcost = newcost;
                        nbuffer[neighborsindex] = node;
                        queryforBFS.Enqueue(neighborsindex);
                    }
                }
            }
            queryforBFS.Dispose();

            status.Value = FieldState.CalculatingDirection;
            statuses[e] = status;
        }
    }
}
}
