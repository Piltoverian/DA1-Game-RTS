using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial struct MatchTime : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<MatchTimeComponent>())
        {
            state.EntityManager.CreateSingleton(new MatchTimeComponent { Value = 0 });
        }
        state.RequireForUpdate<MatchTimeComponent>();

    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!ServerMatch.isGamePlayCanRun(ref state)) return;
        var matchTime = SystemAPI.GetSingletonRW<MatchTimeComponent>();
        matchTime.ValueRW.Value += SystemAPI.Time.DeltaTime;
    }
}
[GhostComponent(OwnerSendType = SendToOwnerType.All)]
public struct MatchTimeComponent : IComponentData
{
    [GhostField]
    public float Value;
}