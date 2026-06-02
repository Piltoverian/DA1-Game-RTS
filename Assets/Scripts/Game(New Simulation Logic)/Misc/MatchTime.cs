using Unity.Entities;
using Unity.Burst;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial struct MatchTime : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.EntityManager.CreateSingleton<MatchTimeComponent>();

    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var matchTime = SystemAPI.GetSingletonRW<MatchTimeComponent>();
        matchTime.ValueRW.Value += SystemAPI.Time.DeltaTime;
    }
}

public struct MatchTimeComponent : IComponentData
{
    public float Value;
}