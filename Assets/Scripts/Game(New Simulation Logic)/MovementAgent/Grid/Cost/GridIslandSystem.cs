using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(GridInitSystem))]
[UpdateBefore(typeof(IntegrationFieldSystem))]
public partial struct GridIslandSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (grid, entity) in SystemAPI.Query<RefRW<GridComponent>>().WithEntityAccess())
        {
            if (grid.ValueRW.islandGeneration == grid.ValueRW.generation)
                continue;
            var costBuffer = SystemAPI.GetBuffer<GridNodeCost>(entity);
            var islandBuffer = SystemAPI.GetBuffer<GridIsland>(entity);
            int width = grid.ValueRO.width;
            int height = grid.ValueRO.height;
            int totalNodes = width * height;

            NativeArray<bool> visited = new NativeArray<bool>(totalNodes, Allocator.Temp);
            NativeQueue<int2> queue = new NativeQueue<int2>(Allocator.Temp);

            int currentIslandID = 0;

            for (int i = 0; i < totalNodes; i++)
            {
               
                if (visited[i] || costBuffer[i].cost == int.MaxValue)
                    continue;

                currentIslandID++;
                int2 startPos = GridHelper.GetGridPosFromIndex(i, grid.ValueRO);
                
                queue.Enqueue(startPos);
                visited[i] = true;
                islandBuffer[i] = new GridIsland { islandID = currentIslandID };

                while (queue.TryDequeue(out int2 currentPos))
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        for (int y = -1; y <= 1; y++)
                        {
                            if (x == 0 && y == 0) continue;

                            int2 neighborPos = currentPos + new int2(x, y);

                            if (neighborPos.x >= 0 && neighborPos.x < width && neighborPos.y >= 0 && neighborPos.y < height)
                            {
                                if (x != 0 && y != 0)
                                {
                                    int2 orthox = currentPos + new int2(x, 0);
                                    int2 orthoy = currentPos + new int2(0, y);
                                    
                                    int indexX = GridHelper.GetNodeIndex(orthox, grid.ValueRO);
                                    int indexY = GridHelper.GetNodeIndex(orthoy, grid.ValueRO);

                                    if (costBuffer[indexX].cost == int.MaxValue || costBuffer[indexY].cost == int.MaxValue)
                                        continue;
                                }

                                int neighborIndex = GridHelper.GetNodeIndex(neighborPos, grid.ValueRO);

                                if (!visited[neighborIndex] && costBuffer[neighborIndex].cost < int.MaxValue)
                                {
                                    visited[neighborIndex] = true;
                                    islandBuffer[neighborIndex] = new GridIsland { islandID = currentIslandID };
                                    queue.Enqueue(neighborPos);
                                }
                            }
                        }
                    }
                }
            }

            visited.Dispose();
            queue.Dispose();
            grid.ValueRW.islandGeneration = grid.ValueRW.generation;
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state) { }
}
