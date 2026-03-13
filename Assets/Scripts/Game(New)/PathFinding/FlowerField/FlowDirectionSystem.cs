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
        var gridEntity = SystemAPI.GetSingletonEntity<GridComponent>();
        var grid = SystemAPI.GetComponent<GridComponent>(gridEntity);
        EntityCommandBuffer ecb= new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        foreach (var(field,_,nodebuffer,entity)in SystemAPI.Query<RefRO<FlowField>, FieldUpdateRequest,DynamicBuffer<FieldNode>>().WithEntityAccess())
        {
            var nbuffer = nodebuffer;
            if (nbuffer.Length == 0)
            {
                continue;
            }
            for (int i = 0; i < nbuffer.Length; i++)
            {
                int2 cell = GridHelper.GetGridPosFromIndex(i,grid);
                int bestcost= nbuffer[i].bestcost;
                float2 bestdirection=float2.zero;
                if(!(cell.x< 0|| cell.x>= grid.width||cell.y<0||cell.y>=grid.height))
                {
                    for( int j=0;j<8;j++)
                    {
                        int2 neighborcell=cell+ neighborsdir[j];

                        if(neighborcell.x < 0 || neighborcell.x >= grid.width || neighborcell.y < 0 || neighborcell.y >= grid.height)
                        {
                            continue;
                        }

                        int neighborindex=GridHelper.GetNodeIndex(neighborcell,grid);
                        int neighborcost= nbuffer[neighborindex].bestcost;
                        if (neighborcost<bestcost)
                        {
                            bestcost=neighborcost;
                            bestdirection = math.normalize(new float2(neighborsdir[j].x, neighborsdir[j].y));
                        }
                    }
                }
                FieldNode node = nbuffer[i];
                node.direction = bestdirection;
                nbuffer[i] = node;
                ecb.RemoveComponent<FieldUpdateRequest>(entity);
            }
            
        }
        ecb.Playback(state.EntityManager);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
