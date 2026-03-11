using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(GridInitSystem))]
// this asume that at the start of the game, all the cost is 1, and then we will update it in the next system

partial struct IntergrationFieldSystem : ISystem
{

    static readonly int2[] neighborsdir =
    {
        new int2 (0,1), new int2 (1,0), new int2 (0,-1), new int2 (-1,0),
        new int2 (1,-1),new int2(-1,1), new int2 (-1,-1), new int2 (1,1),
    };

    static readonly int[] dircost =
    {
        10,10,10,10,
        14,14,14,14
    };

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        
        foreach(var(grid,nodebufferRO,costbufferRO,entity) in SystemAPI.Query<RefRO<GridComponent>,DynamicBuffer<GridNode>,DynamicBuffer<GridNodeCost>>().WithEntityAccess())
        {
            
            var nbuffer = nodebufferRO;
            var cbuffer = costbufferRO;
            if(nbuffer.Length == 0)
            {
                continue;
            }
            for (var i = 0; i < nbuffer.Length; i++)
            {
                GridNode node = nbuffer[i];
                node.bestcost = int.MaxValue;
                nbuffer[i] = node;
            }

            int2 targetcell = new int2(grid.ValueRO.width / 2, grid.ValueRO.height / 2);
            Target target;
            if (state.EntityManager.HasComponent<Target>(entity))
            {
                target = SystemAPI.GetComponent<Target>(entity);

                targetcell = GridHelper.WorldToGrid(target.worldpos, grid.ValueRO);
            }
            int targetindex =GridHelper.GetNodeIndex(targetcell,grid.ValueRO);
            GridNode targetnode = nbuffer[targetindex];
            targetnode.bestcost = 0;
            nbuffer[targetindex] = targetnode;

            NativeQueue<int> queryforBFS = new NativeQueue<int>(Allocator.Temp);
            queryforBFS.Enqueue(targetindex);

            while (queryforBFS.TryDequeue(out int current))
            {
                int2 gridPos=GridHelper.GetGridPosFromIndex(current,grid.ValueRO);
                int currentbestcost = nbuffer[current].bestcost;
                for (int i=0;i<8;i++)
                {
                    int2 neighborsgridpos=gridPos+neighborsdir[i];
                    if (neighborsgridpos.x < 0 ||neighborsgridpos.x>=grid.ValueRO.width
                        || neighborsgridpos.y < 0 || neighborsgridpos.y >= grid.ValueRO.height)
                    {
                        continue;
                    }
                    if(i>3)
                    {
                        int2 orthox = gridPos+new int2(1 * neighborsdir[i].x,0);
                        int2 orthoy = gridPos+new int2(0,neighborsdir[i].y);
                        if (orthox.x < 0 || orthox.x >= grid.ValueRO.width ||
                        orthox.y < 0 || orthox.y >= grid.ValueRO.height)
                            continue;
                        if (orthoy.x < 0 || orthoy.x >= grid.ValueRO.width ||
                       orthoy.y < 0 || orthoy.y >= grid.ValueRO.height)
                            continue;
                        int orthoxindex = GridHelper.GetNodeIndex(orthox, grid.ValueRO);
                        int orthoyindex=GridHelper.GetNodeIndex(orthoy, grid.ValueRO);
                        if (cbuffer[orthoxindex].cost == int.MaxValue || cbuffer[orthoyindex].cost==int.MaxValue)
                        {
                            continue;
                        }
                    }
                    int neighborsindex = GridHelper.GetNodeIndex(neighborsgridpos,grid.ValueRO);
                    int neighborcost=cbuffer[neighborsindex].cost;

                    if (neighborcost==int.MaxValue)
                    {
                        continue;
                    }

                    int newcost=neighborcost+currentbestcost+dircost[i];
                    if (newcost < nbuffer[neighborsindex].bestcost)
                    {
                        GridNode node = nbuffer[neighborsindex];
                        node.bestcost = newcost;
                        nbuffer[neighborsindex] = node;
                        queryforBFS.Enqueue(neighborsindex);
                    }
                }
            }
            queryforBFS.Dispose();
            state.Enabled = false;
        }
        
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
