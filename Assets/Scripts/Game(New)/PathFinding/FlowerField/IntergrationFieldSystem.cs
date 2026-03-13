using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

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
        var gridEntity = SystemAPI.GetSingletonEntity<GridComponent>();
        var grid = SystemAPI.GetComponent<GridComponent>(gridEntity);
        var cbuffer = state.EntityManager.GetBuffer<GridNodeCost>(gridEntity);
        foreach (var(field,_,nodebuffer,entity) in SystemAPI.Query<RefRO<FlowField>,FieldUpdateRequest,DynamicBuffer<FieldNode>>().WithEntityAccess())
        {
            var nbuffer = nodebuffer;
            if (nbuffer.Length == 0)
            {
                continue;
            }
            for (var i = 0; i < nbuffer.Length; i++)
            {
                FieldNode node = nbuffer[i];
                node.bestcost = int.MaxValue;
                nbuffer[i] = node;
            }

            int2 targetcell = new int2(23,54);
            targetcell =field.ValueRO.targetcell;
            int targetindex =GridHelper.GetNodeIndex(targetcell,grid);
            FieldNode targetnode = nbuffer[targetindex];
            targetnode.bestcost = 0;
            nbuffer[targetindex] = targetnode;

            NativeQueue<int> queryforBFS = new NativeQueue<int>(Allocator.Temp);
            queryforBFS.Enqueue(targetindex);

            while (queryforBFS.TryDequeue(out int current))
            {
                int2 gridPos=GridHelper.GetGridPosFromIndex(current,grid);
                int currentbestcost = nbuffer[current].bestcost;
                for (int i=0;i<8;i++)
                {
                    int2 neighborsgridpos=gridPos+neighborsdir[i];
                    if (neighborsgridpos.x < 0 ||neighborsgridpos.x>=grid.width
                        || neighborsgridpos.y < 0 || neighborsgridpos.y >= grid.height)
                    {
                        continue;
                    }
                    if(i>3)
                    {
                        int2 orthox = gridPos+new int2(1 * neighborsdir[i].x,0);
                        int2 orthoy = gridPos+new int2(0,neighborsdir[i].y);
                        if (orthox.x < 0 || orthox.x >= grid.width ||
                        orthox.y < 0 || orthox.y >= grid.height)
                            continue;
                        if (orthoy.x < 0 || orthoy.x >= grid.width ||
                       orthoy.y < 0 || orthoy.y >= grid.height)
                            continue;
                        int orthoxindex = GridHelper.GetNodeIndex(orthox, grid);
                        int orthoyindex=GridHelper.GetNodeIndex(orthoy, grid);
                        if (cbuffer[orthoxindex].cost == int.MaxValue || cbuffer[orthoyindex].cost==int.MaxValue)
                        {
                            continue;
                        }
                    }
                    int neighborsindex = GridHelper.GetNodeIndex(neighborsgridpos,grid);
                    int neighborcost=cbuffer[neighborsindex].cost;

                    if (neighborcost==int.MaxValue)
                    {
                        continue;
                    }

                    int newcost=neighborcost+currentbestcost+dircost[i];
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
            
        }
        
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}


