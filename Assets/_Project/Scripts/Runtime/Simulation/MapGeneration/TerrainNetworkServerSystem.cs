using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

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
        if(!GameplayGhostVisibility.TryGetFog(EntityManager,out var grid,out var fog)
            ||sourceQuery.GetSingletonEntity()!=fog.SourceGrid||source.Revision!=fog.GridRevision
            ||source.Grid.width!=grid.width||source.Grid.height!=grid.height||source.Grid.cellsize!=grid.cellsize
            ||math.any(source.Grid.origin!=grid.origin)||source.Cells==null||source.Cells.Length!=(long)grid.width*grid.height
            ||!fog.Explored.IsCreated||!fog.Visible.IsCreated){
            // Keep already-authorized immutable chunks during a transient unavailable FOW frame.
            return;
        }
        // Disconnect cleanup also works if the Netcode connection entity was already destroyed.
        var removed=new List<Entity>();
        foreach(var pair in streams){
            if(EntityManager.Exists(pair.Key)&&EntityManager.HasComponent<NetworkId>(pair.Key)
                &&!EntityManager.HasComponent<NetworkStreamRequestDisconnect>(pair.Key))continue;
            DestroyChunks(pair.Value);removed.Add(pair.Key);
        }
        foreach(var e in removed)streams.Remove(e);
        using var query=EntityManager.CreateEntityQuery(typeof(NetworkId),typeof(NetworkStreamInGame),typeof(PlayerConnection));
        using var connections=query.ToEntityArray(Allocator.Temp);
        int across=(source.Grid.width+ChunkSide-1)/ChunkSide;
        var byPlayer=new Dictionary<int,SortedDictionary<int,SortedSet<int>>>();
        foreach(var pair in fog.Explored){
            int i=pair.Key;if(i<0||i>=source.Cells.Length)continue;
            int chunk=(i/source.Grid.width/ChunkSide)*across+(i%source.Grid.width/ChunkSide);
            if(!byPlayer.TryGetValue(pair.Value,out var playerGroups))byPlayer.Add(pair.Value,playerGroups=new());
            if(!playerGroups.TryGetValue(chunk,out var playerCells))playerGroups.Add(chunk,playerCells=new());
            playerCells.Add(i);
        }
        foreach(var connection in connections){
            if(EntityManager.HasComponent<NetworkStreamRequestDisconnect>(connection))continue;
            var owner=EntityManager.GetComponentData<PlayerConnection>(connection);
            int networkId=EntityManager.GetComponentData<NetworkId>(connection).Value;
            if(!streams.TryGetValue(connection,out var stream))streams.Add(connection,stream=new PlayerStream());
            if(stream.Epoch!=source.Epoch){
                DestroyChunks(stream);stream.Epoch=source.Epoch;

            }
            if(!byPlayer.TryGetValue(owner.PlayerId,out var groups))groups=new SortedDictionary<int,SortedSet<int>>();
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
                var header=new TerrainChunkGhost{Session=owner.MatchSessionId,JoinGeneration=owner.JoinGeneration,Epoch=source.Epoch,Revision=source.Revision,
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
