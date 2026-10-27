using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

public struct TerrainJoinRequest : IRpcCommand { }
public struct TerrainJoinSent : IComponentData { }
public struct TerrainReadyRequest : IRpcCommand { public uint Session, Epoch; }
public struct TerrainClientReady : IComponentData { public uint Session, Epoch; }

// Public connection -> player mapping; unit networking can reuse it later.
public struct TerrainPlayerConnection : IComponentData
{
    public int PlayerId;
    public uint Session;
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct TerrainJoinClientSystem : ISystem
{
    public void OnCreate(ref SystemState state) { state.RequireForUpdate<TerrainSyncSettings>(); }
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
[UpdateAfter(typeof(TerrainVisualSourceSystem))]
[UpdateBefore(typeof(TerrainNetworkServerSystem))]
public partial class TerrainJoinServerSystem : SystemBase
{
    uint nextSession;
    protected override void OnCreate(){RequireForUpdate<TerrainSyncSettings>();RequireForUpdate<PlayerBootstrapSlot>();}
    protected override void OnUpdate()
    {
        using var slotsQuery=EntityManager.CreateEntityQuery(typeof(PlayerBootstrapSlot));
        if(slotsQuery.CalculateEntityCount()!=1)return;
        using var slots=EntityManager.GetBuffer<PlayerBootstrapSlot>(slotsQuery.GetSingletonEntity(),true).ToNativeArray(Allocator.Temp);
        using var assigned=EntityManager.CreateEntityQuery(typeof(NetworkId),typeof(TerrainPlayerConnection));
        using var players=assigned.ToComponentDataArray<TerrainPlayerConnection>(Allocator.Temp);
        var used=new HashSet<int>();foreach(var player in players)used.Add(player.PlayerId);
        using var requests=EntityManager.CreateEntityQuery(typeof(TerrainJoinRequest),typeof(ReceiveRpcCommandRequest));
        using var entities=requests.ToEntityArray(Allocator.Temp);
        foreach(var request in entities){
            var connection=EntityManager.GetComponentData<ReceiveRpcCommandRequest>(request).SourceConnection;
            if(EntityManager.Exists(connection)&&EntityManager.HasComponent<NetworkId>(connection)
                &&!EntityManager.HasComponent<NetworkStreamRequestDisconnect>(connection)
                &&!EntityManager.HasComponent<TerrainPlayerConnection>(connection)){
                int selected=-1;
                foreach(var slot in slots)if(!used.Contains(slot.PlayerId)&&(selected<0||slot.PlayerId<selected))selected=slot.PlayerId;
                if(selected<0)EntityManager.AddComponent<NetworkStreamRequestDisconnect>(connection);
                else{
                    used.Add(selected);
                    EntityManager.AddComponentData(connection,new TerrainPlayerConnection{PlayerId=selected,Session=++nextSession});
                    if(!EntityManager.HasComponent<NetworkStreamInGame>(connection))EntityManager.AddComponent<NetworkStreamInGame>(connection);
                }
            }
            EntityManager.DestroyEntity(request);
        }
        using var sources=EntityManager.CreateEntityQuery(typeof(TerrainVisualSource));
        if(sources.CalculateEntityCount()!=1)return;
        var source=EntityManager.GetComponentObject<TerrainVisualSource>(sources.GetSingletonEntity());
        using var readyQuery=EntityManager.CreateEntityQuery(typeof(TerrainReadyRequest),typeof(ReceiveRpcCommandRequest));
        using var readyRequests=readyQuery.ToEntityArray(Allocator.Temp);
        foreach(var e in readyRequests){
            var ready=EntityManager.GetComponentData<TerrainReadyRequest>(e);
            var connection=EntityManager.GetComponentData<ReceiveRpcCommandRequest>(e).SourceConnection;
            if(EntityManager.Exists(connection)&&EntityManager.HasComponent<NetworkId>(connection)
                &&!EntityManager.HasComponent<NetworkStreamRequestDisconnect>(connection)&&EntityManager.HasComponent<TerrainPlayerConnection>(connection)
                &&ready.Session==EntityManager.GetComponentData<TerrainPlayerConnection>(connection).Session&&ready.Epoch==source.Epoch){
                var value=new TerrainClientReady{Session=ready.Session,Epoch=ready.Epoch};
                if(EntityManager.HasComponent<TerrainClientReady>(connection))EntityManager.SetComponentData(connection,value);
                else EntityManager.AddComponentData(connection,value);
            }
            EntityManager.DestroyEntity(e);
        }
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(TerrainVisualSourceSystem))]
public partial class TerrainNetworkServerSystem : SystemBase
{
    public const int ChunkSide=8;
    sealed class PlayerStream
    {
        public uint Epoch;
        public readonly Dictionary<int,Entity> Chunks=new();
    }
    readonly Dictionary<Entity,PlayerStream> streams=new();
    double nextSend;
    protected override void OnCreate(){RequireForUpdate<TerrainVisualSource>();RequireForUpdate<FogOfWarSpatialSystemData>();RequireForUpdate<PlayerBootstrapState>();}
    protected override void OnUpdate()
    {
        if(SystemAPI.Time.ElapsedTime<nextSend)return;
        nextSend=SystemAPI.Time.ElapsedTime+.1;
        if(SystemAPI.GetSingleton<PlayerBootstrapState>().Phase!=PlayerBootstrapPhase.Ready)return;
        EntityManager.CompleteAllTrackedJobs();
        using var sourceQuery=EntityManager.CreateEntityQuery(typeof(TerrainVisualSource));
        if(sourceQuery.CalculateEntityCount()!=1)return;
        var source=EntityManager.GetComponentObject<TerrainVisualSource>(sourceQuery.GetSingletonEntity());
        if(source.Settings.Prefab==Entity.Null)return;
        using var fogQuery=EntityManager.CreateEntityQuery(typeof(FogOfWarSpatialSystemData));
        var fog=fogQuery.GetSingleton<FogOfWarSpatialSystemData>();
        if(!fog.Explored.IsCreated||!fog.Visible.IsCreated)return;
        // Disconnect cleanup also works if the Netcode connection entity was already destroyed.
        var removed=new List<Entity>();
        foreach(var pair in streams){
            if(EntityManager.Exists(pair.Key)&&EntityManager.HasComponent<NetworkId>(pair.Key)
                &&!EntityManager.HasComponent<NetworkStreamRequestDisconnect>(pair.Key))continue;
            DestroyChunks(pair.Value);removed.Add(pair.Key);
        }
        foreach(var e in removed)streams.Remove(e);
        using var query=EntityManager.CreateEntityQuery(typeof(NetworkId),typeof(NetworkStreamInGame),typeof(TerrainPlayerConnection));
        using var connections=query.ToEntityArray(Allocator.Temp);
        foreach(var connection in connections){
            if(EntityManager.HasComponent<NetworkStreamRequestDisconnect>(connection))continue;
            var owner=EntityManager.GetComponentData<TerrainPlayerConnection>(connection);
            int networkId=EntityManager.GetComponentData<NetworkId>(connection).Value;
            if(!streams.TryGetValue(connection,out var stream))streams.Add(connection,stream=new PlayerStream());
            if(stream.Epoch!=source.Epoch){
                DestroyChunks(stream);stream.Epoch=source.Epoch;
                if(EntityManager.HasComponent<TerrainClientReady>(connection)){
                    var ready=EntityManager.GetComponentData<TerrainClientReady>(connection);
                    if(ready.Session!=owner.Session||ready.Epoch!=source.Epoch)EntityManager.RemoveComponent<TerrainClientReady>(connection);
                }
            }
            var groups=new SortedDictionary<int,SortedSet<int>>();
            int across=(source.Grid.width+ChunkSide-1)/ChunkSide;
            foreach(var pair in fog.Explored){
                int i=pair.Key;
                if(pair.Value!=owner.PlayerId||i<0||i>=source.Cells.Length)continue;
                int chunk=(i/source.Grid.width/ChunkSide)*across+(i%source.Grid.width/ChunkSide);
                if(!groups.TryGetValue(chunk,out var cells))groups.Add(chunk,cells=new SortedSet<int>());
                cells.Add(i);
            }
            // One empty header lets a player with zero explored cells still finish receiving map metadata.
            if(groups.Count==0)groups.Add(-1,new SortedSet<int>());
            var obsolete=new List<int>();
            foreach(var old in stream.Chunks)if(!groups.ContainsKey(old.Key)){if(EntityManager.Exists(old.Value))EntityManager.DestroyEntity(old.Value);obsolete.Add(old.Key);}
            foreach(int key in obsolete)stream.Chunks.Remove(key);
            float3 spawn=FindSpawn(owner.PlayerId,source);
            foreach(var group in groups){
                if(!stream.Chunks.TryGetValue(group.Key,out var entity)||!EntityManager.Exists(entity)){
                    entity=EntityManager.Instantiate(source.Settings.Prefab);
                    EntityManager.SetComponentData(entity,new GhostOwner{NetworkId=networkId});
                    stream.Chunks[group.Key]=entity;
                }
                var header=new TerrainChunkGhost{Session=owner.Session,Epoch=source.Epoch,Revision=source.Revision,
                    ChunkIndex=group.Key,ExpectedChunks=groups.Count,PlayerId=owner.PlayerId,
                    Width=source.Grid.width,Height=source.Grid.height,CellSize=source.Grid.cellsize,Origin=source.Grid.origin,
                    SpawnPosition=spawn,VisualSeed=source.Settings.VisualSeed,MountainTiers=source.Settings.MountainTiers,
                    CapPixels=source.Settings.CapPixels,GroundRegionCells=source.Settings.GroundRegionCells};
                // Keep immutable terrain buffers unchanged unless exploration adds/removes a cell.
                var cells=EntityManager.GetBuffer<TerrainCellGhostElement>(entity);
                var walls=EntityManager.GetBuffer<TerrainWallVisualData>(entity);
                bool rebuild=cells.Length!=group.Value.Count;
                if(!rebuild){int n=0;foreach(int i in group.Value)if(cells[n++].CellIndex!=i){rebuild=true;break;}}
                if(rebuild){
                    cells.Clear();walls.Clear();
                    foreach(int i in group.Value)source.Cells[i].AppendToGhostBuffers(i,cells,walls,fog.IsVisibleTo(i,owner.PlayerId));
                }else for(int i=0;i<cells.Length;i++){
                    var cell=cells[i];bool visible=fog.IsVisibleTo(cell.CellIndex,owner.PlayerId);
                    if(cell.Visible!=visible){cell.Visible=visible;cells[i]=cell;}
                }
                if(!EntityManager.GetComponentData<TerrainChunkGhost>(entity).Equals(header))EntityManager.SetComponentData(entity,header);
            }
        }
    }
    float3 FindSpawn(int playerId,TerrainVisualSource source)
    {
        using var bases=EntityManager.CreateEntityQuery(typeof(MainBaseTag),typeof(EntityOwner),typeof(LocalTransform));
        using var entities=bases.ToEntityArray(Allocator.Temp);
        foreach(var e in entities)if(EntityManager.GetComponentData<EntityOwner>(e).PlayerID==playerId)return EntityManager.GetComponentData<LocalTransform>(e).Position;
        return source.Grid.origin+new float3(source.Grid.width*.5f*source.Grid.cellsize,0,source.Grid.height*.5f*source.Grid.cellsize);
    }
    void DestroyChunks(PlayerStream stream)
    {
        foreach(var entity in stream.Chunks.Values)if(EntityManager.Exists(entity))EntityManager.DestroyEntity(entity);
        stream.Chunks.Clear();
    }
}
