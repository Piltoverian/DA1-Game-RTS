using System.Diagnostics;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
partial struct ResourceGatherableSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        int count = 0;
        foreach (RefRW<ResourceGatherableComp> transform in SystemAPI.Query<RefRW<ResourceGatherableComp>>())
        {
            count++;
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
