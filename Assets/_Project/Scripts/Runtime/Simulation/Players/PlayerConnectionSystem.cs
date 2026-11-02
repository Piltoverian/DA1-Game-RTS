using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

// Join RPC names are retained for wire compatibility; player/session ownership is shared by all gameplay streams.
public struct TerrainJoinRequest : IRpcCommand { }
public struct TerrainJoinSent : IComponentData { }
// Public connection -> player mapping; unit networking can reuse it later.
public struct PlayerConnection : IComponentData
{
    public int PlayerId;
    public uint Session;
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct PlayerJoinClientSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkId>();
        // shortcut: TerrainSyncSettings marks Main/SubScene readiness; use shared scene readiness when other streams join.
        state.RequireForUpdate<TerrainSyncSettings>();
    }
    public void OnUpdate(ref SystemState state)
    {
        var ecb=new EntityCommandBuffer(Allocator.Temp);
        foreach(var (_,connection) in SystemAPI.Query<RefRO<NetworkId>>().WithNone<TerrainJoinSent>().WithEntityAccess()){
            ecb.AddComponent<TerrainJoinSent>(connection);
            if(!state.EntityManager.HasComponent<NetworkStreamInGame>(connection))ecb.AddComponent<NetworkStreamInGame>(connection);
            var request=ecb.CreateEntity();
            ecb.AddComponent<TerrainJoinRequest>(request);
            ecb.AddComponent(request,new SendRpcCommandRequest{TargetConnection=connection});
        }
        ecb.Playback(state.EntityManager);ecb.Dispose();
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(TerrainNetworkServerSystem))]
public partial class PlayerJoinServerSystem : SystemBase
{
    uint nextSession;
    protected override void OnCreate(){RequireForUpdate<PlayerBootstrapSlot>();}
    protected override void OnUpdate()
    {
        using var slotsQuery=EntityManager.CreateEntityQuery(typeof(PlayerBootstrapSlot));
        if(slotsQuery.CalculateEntityCount()!=1)return;
        using var slots=EntityManager.GetBuffer<PlayerBootstrapSlot>(slotsQuery.GetSingletonEntity(),true).ToNativeArray(Allocator.Temp);
        using var assigned=EntityManager.CreateEntityQuery(typeof(NetworkId),typeof(PlayerConnection));
        using var players=assigned.ToComponentDataArray<PlayerConnection>(Allocator.Temp);
        var used=new HashSet<int>();foreach(var player in players)used.Add(player.PlayerId);
        using var requests=EntityManager.CreateEntityQuery(typeof(TerrainJoinRequest),typeof(ReceiveRpcCommandRequest));
        using var entities=requests.ToEntityArray(Allocator.Temp);
        foreach(var request in entities){
            var connection=EntityManager.GetComponentData<ReceiveRpcCommandRequest>(request).SourceConnection;
            if(EntityManager.Exists(connection)&&EntityManager.HasComponent<NetworkId>(connection)
                &&!EntityManager.HasComponent<NetworkStreamRequestDisconnect>(connection)
                &&!EntityManager.HasComponent<PlayerConnection>(connection)){
                int selected=-1;
                foreach(var slot in slots)if(!used.Contains(slot.PlayerId)&&(selected<0||slot.PlayerId<selected))selected=slot.PlayerId;
                if(selected<0)EntityManager.AddComponent<NetworkStreamRequestDisconnect>(connection);
                else{
                    used.Add(selected);
                    EntityManager.AddComponentData(connection,new PlayerConnection{PlayerId=selected,Session=++nextSession});
                    if(!EntityManager.HasComponent<NetworkStreamInGame>(connection))EntityManager.AddComponent<NetworkStreamInGame>(connection);
                }
            }
            EntityManager.DestroyEntity(request);
        }

    }
}

