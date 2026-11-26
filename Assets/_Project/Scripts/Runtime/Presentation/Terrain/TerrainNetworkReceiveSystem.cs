using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(PresentationSystemGroup),OrderFirst=true)]
[UpdateBefore(typeof(MapTerrainPresentationSystem))]
public partial class TerrainNetworkReceiveSystem : SystemBase
{
    public TerrainView View { get; } = new();
    public TerrainChunkGhost Header { get; private set; }
    Entity connection;
    ulong geometryHash,visibilityHash;
    double nextRead;
    sealed class DecodedChunk
    {
        public ulong GeometryHash, VisibilityHash;
        public readonly Dictionary<int,TerrainCellVisualData> Cells=new();
        public readonly HashSet<int> Visible=new();
    }
    readonly Dictionary<int,DecodedChunk> decodedChunks=new();
    protected override void OnUpdate()
    {
        using var connections=EntityManager.CreateEntityQuery(new EntityQueryDesc{
            All=new[]{ComponentType.ReadOnly<NetworkId>(),ComponentType.ReadOnly<NetworkStreamInGame>()},
            None=new[]{ComponentType.ReadOnly<NetworkStreamRequestDisconnect>()}});
        if(connections.CalculateEntityCount()!=1){Reset();return;}
        var current=connections.GetSingletonEntity();
        if(current!=connection){Reset();connection=current;nextRead=0;}
        if(SystemAPI.Time.ElapsedTime<nextRead)return;
        nextRead=SystemAPI.Time.ElapsedTime+.1;
        EntityManager.CompleteAllTrackedJobs();
        int networkId=EntityManager.GetComponentData<NetworkId>(connection).Value;
        if (!PlayerNetwork.TryLocal(World, out _, out var identity, false)) return;
        if(View.Ready&&(Header.Session!=identity.MatchSessionId||Header.JoinGeneration!=identity.JoinGeneration||Header.Epoch!=identity.Epoch)){
            View.Dispose();decodedChunks.Clear();geometryHash=visibilityHash=0;Header=default;
        }
        using var query=EntityManager.CreateEntityQuery(typeof(TerrainChunkGhost),typeof(GhostOwner),typeof(TerrainCellGhostElement),typeof(TerrainWallVisualData));
        using var ghosts=query.ToEntityArray(Allocator.Temp);
        var chunks=new SortedDictionary<int,Entity>();
        TerrainChunkGhost latest=default;
        foreach(var e in ghosts){
            if(EntityManager.GetComponentData<GhostOwner>(e).NetworkId!=networkId)continue;
            var h=EntityManager.GetComponentData<TerrainChunkGhost>(e);
            if(h.Session==identity.MatchSessionId&&h.JoinGeneration==identity.JoinGeneration&&h.Epoch==identity.Epoch)latest=h;
        }
        if (latest.Session != identity.MatchSessionId || latest.JoinGeneration != identity.JoinGeneration || latest.Epoch != identity.Epoch) return;
        if(latest.Session==0||latest.Width<=0||latest.Height<=0||latest.Width>4096||latest.Height>4096
            ||!(latest.CellSize>0)||!math.isfinite(latest.CellSize)||!math.all(math.isfinite(latest.Origin)))return;
        if(View.Ready&&(Header.Session!=latest.Session||Header.JoinGeneration!=latest.JoinGeneration||Header.Epoch!=latest.Epoch)){
            View.Dispose();decodedChunks.Clear();geometryHash=visibilityHash=0;
        }
        int expected=0;
        foreach(var e in ghosts){
            if(EntityManager.GetComponentData<GhostOwner>(e).NetworkId!=networkId)continue;
            var h=EntityManager.GetComponentData<TerrainChunkGhost>(e);
            if(h.Session!=latest.Session||h.JoinGeneration!=latest.JoinGeneration||h.Epoch!=latest.Epoch||h.Revision!=latest.Revision)continue;
            if(h.Width!=latest.Width||h.Height!=latest.Height||h.CellSize!=latest.CellSize||math.any(h.Origin!=latest.Origin)
                ||h.PlayerId!=latest.PlayerId||h.VisualSeed!=latest.VisualSeed||h.MountainTiers!=latest.MountainTiers||h.CapPixels!=latest.CapPixels||h.GroundRegionCells!=latest.GroundRegionCells)continue;
            expected=math.max(expected,h.ExpectedChunks);
            if(chunks.ContainsKey(h.ChunkIndex))return;
            chunks.Add(h.ChunkIndex,e);
        }
        // Keep the previous complete view while a new page is in flight.
        if(expected<=0||chunks.Count!=expected)return;
        ulong gh=14695981039346656037UL,vh=14695981039346656037UL;
        Mix(ref gh,(uint)latest.Session);Mix(ref gh,(uint)(latest.Session >> 32));Mix(ref gh,latest.JoinGeneration);Mix(ref gh,latest.Epoch);
        var seen=new HashSet<int>(); var visible=new HashSet<int>();
        foreach(var chunk in chunks){
            var cells=EntityManager.GetBuffer<TerrainCellGhostElement>(chunk.Value,true);
            var walls=EntityManager.GetBuffer<TerrainWallVisualData>(chunk.Value,true);
            if(cells.Length>TerrainNetworkServerSystem.ChunkSide*TerrainNetworkServerSystem.ChunkSide)return;
            ulong cg=14695981039346656037UL,cv=14695981039346656037UL;
            foreach(var cell in cells){
                if(!Valid(cell,walls.Length,latest.Width*latest.Height)||!seen.Add(cell.CellIndex))return;
                int across=(latest.Width+TerrainNetworkServerSystem.ChunkSide-1)/TerrainNetworkServerSystem.ChunkSide;
                int key=(cell.CellIndex/latest.Width/TerrainNetworkServerSystem.ChunkSide)*across+cell.CellIndex%latest.Width/TerrainNetworkServerSystem.ChunkSide;
                if(chunk.Key!=key)return;
                Mix(ref cg,(uint)cell.CellIndex);Mix(ref cg,math.hash(cell.CornerHeights));
                Mix(ref cg,math.asuint(cell.TopHeight));Mix(ref cg,cell.TopMask);Mix(ref cg,cell.IsMountain?1u:0u);
                Mix(ref cg,math.asuint(cell.MountainHeight));Mix(ref cg,cell.MountainMask);Mix(ref cg,(uint)cell.WallCount);
                for(int i=0;i<cell.WallCount;i++){
                    var wall=walls[cell.WallStart+i];
                    if(wall.Side>1||wall.Mask>15||!math.isfinite(wall.Height)||!math.isfinite(wall.SortHeight))return;
                    Mix(ref cg,wall.Side);Mix(ref cg,wall.Mask);Mix(ref cg,math.asuint(wall.Height));Mix(ref cg,math.asuint(wall.SortHeight));
                }
                Mix(ref cv,(uint)cell.CellIndex);Mix(ref cv,cell.Visible?1u:0u);
            }
            if(!decodedChunks.TryGetValue(chunk.Key,out var cached))decodedChunks.Add(chunk.Key,cached=new DecodedChunk());
            if(cached.GeometryHash!=cg){
                cached.Cells.Clear();
                foreach(var cell in cells){
                    var segments=new TerrainWallVisualData[cell.WallCount];
                    for(int i=0;i<segments.Length;i++)segments[i]=walls[cell.WallStart+i];
                    cached.Cells.Add(cell.CellIndex,new TerrainCellVisualData{CornerHeights=cell.CornerHeights,TopHeight=cell.TopHeight,TopMask=cell.TopMask,
                        IsMountain=cell.IsMountain,MountainHeight=cell.MountainHeight,MountainMask=cell.MountainMask,Walls=segments});
                }
                cached.GeometryHash=cg;
            }
            if(cached.VisibilityHash!=cv){
                cached.Visible.Clear();foreach(var cell in cells)if(cell.Visible)cached.Visible.Add(cell.CellIndex);
                cached.VisibilityHash=cv;
            }
            foreach(int cell in cached.Visible)visible.Add(cell);
            Mix(ref gh,(uint)chunk.Key);Mix(ref gh,(uint)cg);Mix(ref gh,(uint)(cg>>32));
            Mix(ref vh,(uint)chunk.Key);Mix(ref vh,(uint)cv);Mix(ref vh,(uint)(cv>>32));
        }
        if(!View.Ready||geometryHash!=gh){
            // Reuse decoded records for unchanged chunks; pending renderer builds retain their immutable dictionary.
            var decoded=new Dictionary<int,TerrainCellVisualData>(seen.Count);
            foreach(int key in chunks.Keys)foreach(var cell in decodedChunks[key].Cells)decoded.Add(cell.Key,cell.Value);
            View.SetGeometry(new GridComponent { width=latest.Width,height=latest.Height,cellsize=latest.CellSize,origin=latest.Origin },
                new TerrainSyncSettings { VisualSeed=latest.VisualSeed,MountainTiers=latest.MountainTiers,CapPixels=latest.CapPixels,GroundRegionCells=latest.GroundRegionCells },decoded);
            var obsolete=new List<int>();foreach(int key in decodedChunks.Keys)if(!chunks.ContainsKey(key))obsolete.Add(key);
            foreach(int key in obsolete)decodedChunks.Remove(key);
        }
        if (!View.Ready || visibilityHash != vh || geometryHash != gh) View.UpdateVisibility(visible);
        geometryHash=gh;visibilityHash=vh;Header=latest;View.Ready=true;
    }
    static bool Valid(TerrainCellGhostElement c,int walls,int size)=>c.CellIndex>=0&&c.CellIndex<size
        &&c.TopMask<=15&&c.MountainMask<=15&&math.all(math.isfinite(c.CornerHeights))&&math.isfinite(c.TopHeight)&&math.isfinite(c.MountainHeight)
        &&c.WallStart>=0&&c.WallCount>=0&&c.WallStart<=walls&&c.WallCount<=walls-c.WallStart;
    static void Mix(ref ulong hash,uint v){unchecked{hash=(hash^v)*1099511628211UL;}}
    void Reset(){decodedChunks.Clear();if(connection!=Entity.Null||View.Ready){View.Dispose();decodedChunks.Clear();geometryHash=visibilityHash=0;Header=default;}connection=Entity.Null;}
    protected override void OnDestroy(){View.Dispose();}
}
