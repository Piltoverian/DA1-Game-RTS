using Unity.Burst;
using Unity.Entities;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(PlayerBootstrapSystem))]
partial struct MatchLifecycleSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<MatchState>();
        state.RequireForUpdate<PlayerBootstrapState>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (SystemAPI.GetSingleton<MatchState>().Phase == MatchPhase.Preparing&&SystemAPI.GetSingleton<PlayerBootstrapState>().Phase==PlayerBootstrapPhase.Ready)
        {
            ServerMatch.SetPhase(ref state, MatchPhase.WaitingForPlayers);
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
