using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(IntegrationFieldSystem))]
partial struct FlowDirectionSystem : ISystem
{

    private EntityQuery m_FieldQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        var builder = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<FlowField>()
            .WithAllRW<FlowFieldStatus>()
            .WithAllRW<FieldNode>();
        m_FieldQuery = state.GetEntityQuery(builder);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<GridComponent>())
            return;

        var gridEntity = SystemAPI.GetSingletonEntity<GridComponent>();
        var grid = SystemAPI.GetComponent<GridComponent>(gridEntity);

        NativeArray<int2> nativeNeighbors = new NativeArray<int2>(8, Allocator.TempJob);
        nativeNeighbors[0] = new int2(0, 1);
        nativeNeighbors[1] = new int2(1, 0);
        nativeNeighbors[2] = new int2(0, -1);
        nativeNeighbors[3] = new int2(-1, 0);
        nativeNeighbors[4] = new int2(1, -1);
        nativeNeighbors[5] = new int2(-1, 1);
        nativeNeighbors[6] = new int2(-1, -1);
        nativeNeighbors[7] = new int2(1, 1);

        var job = new CalculateDirectionFieldJob
        {
            Grid = grid,
            NeighborsDir = nativeNeighbors,
            FlowFieldType = SystemAPI.GetComponentTypeHandle<FlowField>(true),
            StatusType = SystemAPI.GetComponentTypeHandle<FlowFieldStatus>(false),
            NodeType = SystemAPI.GetBufferTypeHandle<FieldNode>(false)
        };

        state.Dependency = job.ScheduleParallel(m_FieldQuery, state.Dependency);

        nativeNeighbors.Dispose(state.Dependency);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
    }
}

[BurstCompile]
public struct CalculateDirectionFieldJob : IJobChunk
{
    [ReadOnly] public GridComponent Grid;
    [ReadOnly] public NativeArray<int2> NeighborsDir;

    [ReadOnly] public ComponentTypeHandle<FlowField> FlowFieldType;
    public ComponentTypeHandle<FlowFieldStatus> StatusType;
    public BufferTypeHandle<FieldNode> NodeType;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
    {
        NativeArray<FlowFieldStatus> statuses = chunk.GetNativeArray(ref StatusType);
        BufferAccessor<FieldNode> nodeBuffers = chunk.GetBufferAccessor(ref NodeType);

        for (int e = 0; e < chunk.Count; e++)
        {
            if (statuses[e].Value != FieldState.CalculatingDirection)
                continue;

            FlowFieldStatus status = statuses[e];
            DynamicBuffer<FieldNode> nbuffer = nodeBuffers[e];

            if (nbuffer.Length > 0)
            {
                for (int i = 0; i < nbuffer.Length; i++)
                {
                    int2 cell = GridHelper.GetGridPosFromIndex(i, Grid);
                    int bestcost = nbuffer[i].bestcost;
                    float2 bestdirection = float2.zero;

                    if (!(cell.x < 0 || cell.x >= Grid.width || cell.y < 0 || cell.y >= Grid.height))
                    {
                        for (int j = 0; j < 8; j++)
                        {
                            int2 neighborcell = cell + NeighborsDir[j];

                            if (neighborcell.x < 0 || neighborcell.x >= Grid.width || neighborcell.y < 0 || neighborcell.y >= Grid.height)
                                continue;

                            int neighborindex = GridHelper.GetNodeIndex(neighborcell, Grid);
                            int neighborcost = nbuffer[neighborindex].bestcost;

                            if (neighborcost < bestcost)
                            {
                                bestcost = neighborcost;
                                bestdirection = math.normalize(new float2(NeighborsDir[j].x, NeighborsDir[j].y));
                            }
                        }
                    }
                    
                    FieldNode node = nbuffer[i];
                    node.direction = bestdirection;
                    nbuffer[i] = node;
                }
            }

            status.Value = FieldState.Ready;
            statuses[e] = status;
        }
    }
}
