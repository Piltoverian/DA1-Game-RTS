using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.NetCode;

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

    public void OnUpdate(ref SystemState state)
    {
        if (SystemAPI.GetSingleton<MatchState>().Phase == MatchPhase.Preparing&&SystemAPI.GetSingleton<PlayerBootstrapState>().Phase==PlayerBootstrapPhase.Ready)
        {
            ServerMatch.SetPhase(ref state, MatchPhase.WaitingForPlayers);
        }
        if ((state.WorldUnmanaged.Flags & WorldFlags.GameServer) != WorldFlags.GameServer
            || SystemAPI.GetSingleton<MatchState>().Phase != MatchPhase.WaitingForPlayers
            || !SystemAPI.TryGetSingleton<MatchSession>(out var session)) return;
        var em = state.EntityManager;
        if (!PlayerSessionSlots.Ensure(em, session.Id, out var root)) return;
        uint currentEpoch = 0;
        using var sourceQuery = em.CreateEntityQuery(typeof(TerrainVisualSource));
        if (sourceQuery.CalculateEntityCount() == 1 && GameplayGhostVisibility.TryGetFog(em, out _, out var currentFog))
        {
            var source = em.GetComponentObject<TerrainVisualSource>(sourceQuery.GetSingletonEntity());
            if (source.Revision == currentFog.GridRevision) currentEpoch = source.Epoch;
        }
        if (PlayerSessionSlots.AdvanceWaiting(em, root, PlayerProtocolClock.Now(em), currentEpoch))
            ServerMatch.SetPhase(ref state, MatchPhase.Running);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
