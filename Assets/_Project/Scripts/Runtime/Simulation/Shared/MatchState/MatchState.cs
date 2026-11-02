using UnityEngine;
using Unity.Entities;
using Unity.Burst;
using Unity.NetCode;
public enum  MatchPhase
{
    Preparing,
    WaitingForPlayers,
    Running,
    Finished
}

[GhostComponent(OwnerSendType = SendToOwnerType.All)]
public struct MatchState : IComponentData
{
    [GhostField]
    public MatchPhase Phase;
}

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation|WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(InitializationSystemGroup))]
partial struct MatchStateInitSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        ServerMatch.Initialize(ref state);
        state.Enabled = false;
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        ServerMatch.Shutdown(ref state);
    }
}

