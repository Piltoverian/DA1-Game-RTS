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
    protected override void OnUpdate()
    {
        using var connections=EntityManager.CreateEntityQuery(typeof(NetworkId),typeof(NetworkStreamInGame));
        if(connections.CalculateEntityCount()!=1){Reset();return;}
        var current=connections.GetSingletonEntity();
        if(current!=connection){Reset();connection=current;nextRead=0;}
        if(SystemAPI.Time.ElapsedTime<nextRead)return;
        nextRead=SystemAPI.Time.ElapsedTime+.1;
        EntityManager.CompleteAllTrackedJobs();
        int networkId=EntityManager.GetComponentData<NetworkId>(connection).Value;
        using var query=EntityManager.CreateEntityQuery(typeof(TerrainChunkGhost),typeof(GhostOwner),typeof(TerrainCellGhostElement),typeof(TerrainWallVisualData));
        using var ghosts=query.ToEntityArray(Allocator.Temp);
        var chunks=new SortedDictionary<int,Entity>();
        TerrainChunkGhost latest=default;
        foreach(var e in ghosts){
            if(EntityManager.GetComponentData<GhostOwner>(e).NetworkId!=networkId)continue;
            var h=EntityManager.GetComponentData<TerrainChunkGhost>(e);
            if(h.Session>latest.Session||(h.Session==latest.Session&&h.Epoch>latest.Epoch))latest=h;
        }
        if(latest.Session==0||latest.Width<=0||latest.Height<=0||latest.Width>4096||latest.Height>4096
            ||!(latest.CellSize>0)||!math.isfinite(latest.CellSize)||!math.all(math.isfinite(latest.Origin)))return;
        if(View.Ready&&(Header.Session!=latest.Session||Header.Epoch!=latest.Epoch)){
            View.Dispose();geometryHash=visibilityHash=0;
        }
        int expected=0;
        foreach(var e in ghosts){
            if(EntityManager.GetComponentData<GhostOwner>(e).NetworkId!=networkId)continue;
            var h=EntityManager.GetComponentData<TerrainChunkGhost>(e);
            if(h.Session!=latest.Session||h.Epoch!=latest.Epoch)continue;
            if(h.Width!=latest.Width||h.Height!=latest.Height||h.CellSize!=latest.CellSize||math.any(h.Origin!=latest.Origin)
                ||h.PlayerId!=latest.PlayerId||h.VisualSeed!=latest.VisualSeed||h.MountainTiers!=latest.MountainTiers||h.CapPixels!=latest.CapPixels||h.GroundRegionCells!=latest.GroundRegionCells)continue;
            expected=math.max(expected,h.ExpectedChunks);
            if(chunks.ContainsKey(h.ChunkIndex))return;
            chunks.Add(h.ChunkIndex,e);
        }
        // Keep the previous complete view while a new page is in flight.
        if(expected<=0||chunks.Count!=expected)return;
        ulong gh=14695981039346656037UL,vh=14695981039346656037UL;
        Mix(ref gh,latest.Session);Mix(ref gh,latest.Epoch);
        var seen=new HashSet<int>();
        var visible=new HashSet<int>();
        foreach(var chunk in chunks){
            var cells=EntityManager.GetBuffer<TerrainCellGhostElement>(chunk.Value,true);
            var walls=EntityManager.GetBuffer<TerrainWallVisualData>(chunk.Value,true);
            if(cells.Length>TerrainNetworkServerSystem.ChunkSide*TerrainNetworkServerSystem.ChunkSide)return;
            foreach(var cell in cells){
                if(!Valid(cell,walls.Length,latest.Width*latest.Height)||!seen.Add(cell.CellIndex))return;
                int across=(latest.Width+TerrainNetworkServerSystem.ChunkSide-1)/TerrainNetworkServerSystem.ChunkSide;
                int key=(cell.CellIndex/latest.Width/TerrainNetworkServerSystem.ChunkSide)*across+cell.CellIndex%latest.Width/TerrainNetworkServerSystem.ChunkSide;
                if(chunk.Key!=key)return;
                Mix(ref gh,(uint)cell.CellIndex);Mix(ref gh,math.hash(cell.CornerHeights));
                Mix(ref gh,math.asuint(cell.TopHeight));Mix(ref gh,cell.TopMask);Mix(ref gh,cell.IsMountain?1u:0u);
                Mix(ref gh,math.asuint(cell.MountainHeight));Mix(ref gh,cell.MountainMask);Mix(ref gh,(uint)cell.WallCount);
                for(int i=0;i<cell.WallCount;i++){
                    var wall=walls[cell.WallStart+i];
                    if(wall.Side>1||wall.Mask>15||!math.isfinite(wall.Height)||!math.isfinite(wall.SortHeight))return;
                    Mix(ref gh,wall.Side);Mix(ref gh,wall.Mask);Mix(ref gh,math.asuint(wall.Height));Mix(ref gh,math.asuint(wall.SortHeight));
                }
                Mix(ref vh,(uint)cell.CellIndex);Mix(ref vh,cell.Visible?1u:0u);
                if(cell.Visible)visible.Add(cell.CellIndex);
            }
        }
        if(!View.Ready||geometryHash!=gh){
            var decoded=new Dictionary<int,TerrainCellVisualData>(seen.Count);
            foreach(var e in chunks.Values){
                var cells=EntityManager.GetBuffer<TerrainCellGhostElement>(e,true);
                var walls=EntityManager.GetBuffer<TerrainWallVisualData>(e,true);
                foreach(var cell in cells){
                    var segments=new TerrainWallVisualData[cell.WallCount];
                    for(int i=0;i<segments.Length;i++)segments[i]=walls[cell.WallStart+i];
                    decoded.Add(cell.CellIndex,new TerrainCellVisualData{
                        CornerHeights=cell.CornerHeights,TopHeight=cell.TopHeight,TopMask=cell.TopMask,
                        IsMountain=cell.IsMountain,MountainHeight=cell.MountainHeight,MountainMask=cell.MountainMask,Walls=segments});
                }
            }
            View.SetGeometry(new GridComponent { width = latest.Width, height = latest.Height, cellsize = latest.CellSize, origin = latest.Origin },
                new TerrainSyncSettings { VisualSeed = latest.VisualSeed, MountainTiers = latest.MountainTiers, CapPixels = latest.CapPixels, GroundRegionCells = latest.GroundRegionCells }, decoded);
        }
        if (!View.Ready || visibilityHash != vh || geometryHash != gh) View.UpdateVisibility(visible);
        geometryHash=gh;visibilityHash=vh;Header=latest;View.Ready=true;
    }
    static bool Valid(TerrainCellGhostElement c,int walls,int size)=>c.CellIndex>=0&&c.CellIndex<size
        &&c.TopMask<=15&&c.MountainMask<=15&&math.all(math.isfinite(c.CornerHeights))&&math.isfinite(c.TopHeight)&&math.isfinite(c.MountainHeight)
        &&c.WallStart>=0&&c.WallCount>=0&&c.WallStart<=walls&&c.WallCount<=walls-c.WallStart;
    static void Mix(ref ulong hash,uint v){unchecked{hash=(hash^v)*1099511628211UL;}}
    void Reset(){if(connection!=Entity.Null||View.Ready){View.Dispose();geometryHash=visibilityHash=0;Header=default;}connection=Entity.Null;}
    protected override void OnDestroy(){View.Dispose();}
}
