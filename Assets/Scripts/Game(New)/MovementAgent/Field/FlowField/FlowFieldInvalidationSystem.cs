using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(GridIslandSystem))]
[UpdateBefore(typeof(IntegrationFieldSystem))]
partial struct FlowFieldInvalidationSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var grid = SystemAPI.GetSingletonRW<GridComponent>();
        NativeList<StaleFieldEntry> flowfieldEntities = new NativeList<StaleFieldEntry>(Allocator.Temp);
        foreach (var (flowfield, status,refcount, entity) in SystemAPI.Query<RefRW<FlowField>, RefRW<FlowFieldStatus>,RefRW<FlowFieldRefCount>>().WithEntityAccess())
        {
            if (flowfield.ValueRO.gridgeneration == grid.ValueRW.generation) continue;
            if (status.ValueRO.Value!=FieldState.Ready) continue;
            flowfieldEntities.Add(new StaleFieldEntry { entity=entity,refCount=refcount.ValueRW.value});
        }
        if(flowfieldEntities.Length==0)
        {
            grid.ValueRW.RecalcPerframe = 0;
            return;
        }
        if (grid.ValueRW.RecalcPerframe == 0)
            grid.ValueRW.RecalcPerframe = math.max(1, (int)math.ceil(flowfieldEntities.Length / (float)CostChangeSystem.HEARTBEAT_INTERVAL));
        flowfieldEntities.Sort();
        int ToRecalc = math.min(grid.ValueRW.RecalcPerframe, flowfieldEntities.Length);
        for(int i=0;i<ToRecalc;i++)
        {
            var entry = flowfieldEntities[i];
            var status = SystemAPI.GetComponentRW<FlowFieldStatus>(entry.entity);
            status.ValueRW.Value = FieldState.PendingRecalculation;
            var field = SystemAPI.GetComponentRW<FlowField>(entry.entity);
            field.ValueRW.gridgeneration = grid.ValueRW.generation;
        }
        flowfieldEntities.Dispose();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}

struct StaleFieldEntry : System.IComparable<StaleFieldEntry>
{
    public Entity entity;
    public int refCount;
    public int CompareTo(StaleFieldEntry other)
    {
        return other.refCount.CompareTo(refCount); // Giảm dần
    }
}
