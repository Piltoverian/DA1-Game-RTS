using Unity.Burst;
using Unity.Entities;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
partial struct FrameCountSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        Entity cache = state.EntityManager.CreateSingleton(new FrameCount
        { value = 0 });
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var count = SystemAPI.GetSingleton<FrameCount>();
        count.value++;
        SystemAPI.SetSingleton<FrameCount>(count);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
partial struct FixedFrameCountSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        Entity cache = state.EntityManager.CreateSingleton(new FixedFrameCount
        { value=0});
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var count =SystemAPI.GetSingleton<FixedFrameCount>();
        count.value++;
        SystemAPI.SetSingleton<FixedFrameCount>(count);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {

    }
}
