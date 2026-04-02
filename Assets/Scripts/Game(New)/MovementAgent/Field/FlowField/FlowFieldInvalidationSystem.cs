using Unity.Burst;
using Unity.Entities;

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
        var grid = SystemAPI.GetSingleton<GridComponent>();
        foreach (var (flowfield,status) in SystemAPI.Query<RefRW<FlowField>, RefRW<FlowFieldStatus>>())
        {
            if (flowfield.ValueRO.gridgeneration == grid.generation) continue;
            if (status.ValueRO.Value!=FieldState.Ready) continue;
            status.ValueRW.Value = FieldState.PendingRecalculation;
            flowfield.ValueRW.gridgeneration = grid.generation;
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
