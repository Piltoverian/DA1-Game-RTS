using TMPro;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(IntergrationFieldSystem))]
partial struct FlowDirectionSystem : ISystem
{
    static readonly int2[] neighborsdir =
    {
        new int2 (0,1), new int2 (1,0), new int2 (0,-1), new int2 (-1,0),
        new int2 (1,-1),new int2(-1,1), new int2 (-1,-1), new int2 (1,1)
    };

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach(var(grid,nodebufferRO,costbufferRO)in SystemAPI.Query<RefRO<GridComponent>, DynamicBuffer<GridNode>, DynamicBuffer<GridNodeCost>>())
        {
            var nbuffer = nodebufferRO;
            var cbuffer = costbufferRO;
            if (nbuffer.Length == 0)
            {
                return;
            }
            for (int i = 0; i < nbuffer.Length; i++)
            {
                int2 cell = GridHelper.GetGridPosFromIndex(i,grid.ValueRO);
                int bestcost= nbuffer[i].bestcost;
                float2 bestdirection=float2.zero;
                if(!(cell.x< 0|| cell.x>= grid.ValueRO.width||cell.y<0||cell.y>=grid.ValueRO.height))
                {
                    for( int j=0;j<8;j++)
                    {
                        int2 neighborcell=cell+ neighborsdir[j];

                        if(neighborcell.x < 0 || neighborcell.x >= grid.ValueRO.width || neighborcell.y < 0 || neighborcell.y >= grid.ValueRO.height)
                        {
                            continue;
                        }

                        int neighborindex=GridHelper.GetNodeIndex(neighborcell,grid.ValueRO);
                        int neighborcost= nbuffer[neighborindex].bestcost;
                        if (neighborcost<bestcost)
                        {
                            bestcost=neighborcost;
                            bestdirection = math.normalize(new float2(neighborsdir[j].x, neighborsdir[j].y));
                        }
                    }
                }
                GridNode node = nbuffer[i];
                node.direction = bestdirection;
                nbuffer[i] = node;
            }
        }
        state.Enabled = false;
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
