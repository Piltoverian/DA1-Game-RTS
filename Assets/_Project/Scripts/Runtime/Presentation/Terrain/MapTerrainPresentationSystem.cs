using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

public enum MapPresentationPhase : byte { WaitingTerrain, Ready, Failed }
public struct MapPresentationState : IComponentData
{
    public MapPresentationPhase Phase;
    public uint Revision;
    public FixedString512Bytes Error;
}
public sealed class MapPresentationBinding : IComponentData
{
    public TerrainMapRenderer Owner;
    public LocalBaseCameraSnapper Snapper;
    public TerrainChunkRenderer Renderer;
    public GeneratedTerrain Map;
    public int FogScope = -1, KnownCells = -1;
    public void Release()
    {
        if(Renderer){Renderer.Clear();if(Application.isPlaying)UnityEngine.Object.Destroy(Renderer);else UnityEngine.Object.DestroyImmediate(Renderer);}
        Renderer=null;Map=null;
    }
}

// Unity graphics calls stay on the main thread, in the client presentation world.
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation|WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(PresentationSystemGroup),OrderFirst=true)]
[UpdateAfter(typeof(LocalFogPresentationSystem))]
public partial class MapTerrainPresentationSystem : SystemBase
{
    protected override void OnCreate(){RequireForUpdate<MapPresentationBinding>();}
    protected override void OnUpdate()
    {
        if(World.IsClient()){UpdateNetworkTerrain();return;}
        using var grids=EntityManager.CreateEntityQuery(typeof(GridComponent),typeof(GridTerrain));
        if(grids.CalculateEntityCount()!=1)return;
        var ge=grids.GetSingletonEntity();var grid=EntityManager.GetComponentData<GridComponent>(ge);
        uint revision=0;
        if(EntityManager.HasComponent<MapBuildState>(ge)){
            var mapState=EntityManager.GetComponentData<MapBuildState>(ge);
            if(mapState.Phase==MapBuildPhase.Requested||mapState.Phase==MapBuildPhase.Failed)return;
            revision=mapState.Revision;
        }
        var cells=EntityManager.GetBuffer<GridTerrain>(ge);if(cells.Length!=grid.width*grid.height)return;
        var fog=World.GetExistingSystemManaged<LocalFogPresentationSystem>();
        using var bindings=EntityManager.CreateEntityQuery(typeof(MapPresentationBinding),typeof(MapPresentationState));
        using var entities=bindings.ToEntityArray(Allocator.Temp);
        foreach(var entity in entities){
            var state=EntityManager.GetComponentData<MapPresentationState>(entity);
            var binding=EntityManager.GetComponentObject<MapPresentationBinding>(entity);
            if(!binding.Owner||!binding.Owner.isActiveAndEnabled)continue;
            if(fog!=null&&!fog.Frame.IsValid){binding.Release();binding.KnownCells=-1;continue;}
            if(fog!=null&&binding.Renderer)binding.Renderer.SetFog(fog.Mask);
            if(state.Phase==MapPresentationPhase.Failed&&state.Revision==revision
                &&(fog==null||(binding.FogScope==fog.ScopeVersion&&binding.KnownCells==fog.View.Terrain.Cells.Count)))continue;
            if(state.Phase==MapPresentationPhase.Ready&&state.Revision==revision&&binding.Renderer
                &&(fog==null||(binding.FogScope==fog.ScopeVersion&&binding.KnownCells==fog.View.Terrain.Cells.Count)))continue;
            var camera=binding.Owner.ResolveCamera();if(!camera)continue;
            bool firstBuild=state.Phase!=MapPresentationPhase.Ready;
            // Remember failed inputs too: retry on new fog data, not every frame.
            if(fog!=null){binding.FogScope=fog.ScopeVersion;binding.KnownCells=fog.View.Terrain.Cells.Count;}
            try{
                using var probe=RuntimePerformanceProbe.Measure("terrainCompileAndBuild");
                if(!binding.Owner.theme)throw new InvalidOperationException("Assign TerrainMapRenderer.theme");
                binding.Release();
                var map=new GeneratedTerrain{grid=grid,cells=new GridTerrain[cells.Length],spawns=Array.Empty<int2>()};
                for(int i=0;i<cells.Length;i++)map.cells[i]=cells[i];
                // Local stand-in for the server: resolve neighbour-dependent walls,
                // legacy ramps and mountain shapes against the complete authoritative map.
                IsometricTerrainCamera.Configure(camera);
                var theme=binding.Owner.theme;
                if(!theme.IsRenderReady(out string reason))throw new InvalidOperationException(reason);
                float rise=theme.ResolveTierRise(grid.cellsize,camera.transform.forward);
                float capRise=rise*Mathf.Max(1,(theme.mountainCaps[0].height-96)/32f);
                TerrainCellVisualData[] compiled;
                if(EntityManager.HasComponent<TerrainSyncSettings>(ge)){
                    if(!EntityManager.HasComponent<TerrainVisualSource>(ge))continue;
                    var source=EntityManager.GetComponentObject<TerrainVisualSource>(ge);
                    if(source.Revision!=revision)continue;
                    ValidateTheme(theme,source.Settings.VisualSeed,source.Settings.MountainTiers,source.Settings.CapPixels,source.Settings.GroundRegionCells);
                    compiled=source.Cells;
                }else{
                    // Existing offline scenes without replication settings retain their preview path.
                    TerrainRampCompiler.Compile(map);
                    compiled=TerrainCellVisualCompiler.Compile(map,rise,capRise,theme.mountainVisualTiers,theme.groundVisualSeed);
                }
                var visualCells=new Dictionary<int,TerrainCellVisualData>();
                if(fog==null){for(int i=0;i<compiled.Length;i++)visualCells.Add(i,compiled[i]);}
                else {
                    // Only authorized, self-contained payloads reach the renderer.
                    foreach(var cell in fog.View.Terrain.Cells)visualCells.Add(cell.Key,compiled[cell.Key]);
                    // Placeholder cells carry only the shared height datum; their
                    // actual terrain remains unknown and no sprites are emitted.
                    for(int i=0;i<map.cells.Length;i++){
                        map.cells[i]=new GridTerrain{heightLevel=fog.Frame.TerrainBaseLevel};
                        if(map.rampFaceDirections!=null&&map.rampFaceDirections.Length>i)map.rampFaceDirections[i]=-1;
                    }
                    foreach(var cell in fog.View.Terrain.Cells)map.cells[cell.Key]=cell.Value;
                }
                if(EntityManager.HasBuffer<GridSpawnCell>(ge)){
                    var anchors=EntityManager.GetBuffer<GridSpawnCell>(ge);map.spawns=new int2[anchors.Length];
                    for(int i=0;i<anchors.Length;i++)map.spawns[i]=anchors[i].cell;
                }
                binding.Renderer=binding.Owner.gameObject.AddComponent<TerrainChunkRenderer>();
                binding.Renderer.Build(map,binding.Owner.theme,camera,World,visualCells);
                if(fog!=null){binding.Renderer.SetFog(fog.Mask);binding.FogScope=fog.ScopeVersion;binding.KnownCells=fog.View.Terrain.Cells.Count;}
                binding.Map=map;
                state=new MapPresentationState{Phase=MapPresentationPhase.Ready,Revision=revision};
                if(firstBuild)EntityManager.SetComponentEnabled<CameraSnapPending>(entity,true);
            }catch(Exception error){binding.Release();state=new MapPresentationState{Phase=MapPresentationPhase.Failed,Revision=revision,Error=new FixedString512Bytes(error.Message.Length>400?error.Message.Substring(0,400):error.Message)};Debug.LogException(error);}
            EntityManager.SetComponentData(entity,state);
        }
    }
    void UpdateNetworkTerrain()
    {
        var receiver=World.GetExistingSystemManaged<TerrainNetworkReceiveSystem>();
        var view=receiver?.View;
        using var query=EntityManager.CreateEntityQuery(typeof(MapPresentationBinding),typeof(MapPresentationState));
        using var entities=query.ToEntityArray(Allocator.Temp);
        foreach(var e in entities){
            var binding=EntityManager.GetComponentObject<MapPresentationBinding>(e);
            var status=EntityManager.GetComponentData<MapPresentationState>(e);
            if(view==null||!view.Ready){
                binding.Release();
                if(status.Phase!=MapPresentationPhase.WaitingTerrain)EntityManager.SetComponentData(e,new MapPresentationState{Phase=MapPresentationPhase.WaitingTerrain});
                continue;
            }
            if(!binding.Owner||!binding.Owner.isActiveAndEnabled)continue;
            if(binding.Renderer)binding.Renderer.SetFog(view.Mask);
            if(status.Revision==view.GeometryVersion&&(binding.Renderer||status.Phase==MapPresentationPhase.Failed))continue;
            var camera=binding.Owner.ResolveCamera();if(!camera)continue;
            bool first=status.Phase!=MapPresentationPhase.Ready;
            try{
                var h=view.Header;var theme=binding.Owner.theme;
                if(!theme||!theme.IsRenderReady(out _))throw new InvalidOperationException("Terrain theme is missing or not render ready.");
                ValidateTheme(theme,h.VisualSeed,h.MountainTiers,h.CapPixels,h.GroundRegionCells);
                IsometricTerrainCamera.Configure(camera);
                // Unknown cells have no sprites; their backdrop must match local FOW.
                camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=Color.black;
                var grid=new GridComponent{width=h.Width,height=h.Height,cellsize=h.CellSize,origin=h.Origin};
                var map=new GeneratedTerrain{grid=grid,cells=new GridTerrain[h.Width*h.Height],spawns=Array.Empty<int2>(),rampFaceDirections=Array.Empty<int>()};
                binding.Release();
                binding.Renderer=binding.Owner.gameObject.AddComponent<TerrainChunkRenderer>();
                binding.Renderer.Build(map,theme,camera,World,view.Cells);
                binding.Renderer.SetFog(view.Mask);binding.Map=map;
                status=new MapPresentationState{Phase=MapPresentationPhase.Ready,Revision=view.GeometryVersion};
                // A client has no authoritative base entity yet. Use only its server-assigned anchor.
                if(first&&binding.Snapper&&binding.Snapper.snapOnReady)binding.Snapper.Snap(camera,h.SpawnPosition);
                EntityManager.SetComponentEnabled<CameraSnapPending>(e,false);
            }catch(Exception error){
                binding.Release();view.Error=error.Message;
                status=new MapPresentationState{Phase=MapPresentationPhase.Failed,Revision=view.GeometryVersion,
                    Error=new FixedString512Bytes(error.Message.Length>400?error.Message.Substring(0,400):error.Message)};
                Debug.LogException(error);
            }
            EntityManager.SetComponentData(e,status);
            if(status.Phase==MapPresentationPhase.Ready)ReportTerrainReady(view.Header);
        }
    }
    void ReportTerrainReady(TerrainChunkGhost header)
    {
        using var query=EntityManager.CreateEntityQuery(typeof(NetworkId),typeof(NetworkStreamInGame));
        if(query.CalculateEntityCount()!=1)return;
        var connection=query.GetSingletonEntity();
        if(EntityManager.HasComponent<TerrainClientReady>(connection)){
            var previous=EntityManager.GetComponentData<TerrainClientReady>(connection);
            if(previous.Session==header.Session&&previous.Epoch==header.Epoch)return;
        }
        var value=new TerrainClientReady{Session=header.Session,Epoch=header.Epoch};
        if(EntityManager.HasComponent<TerrainClientReady>(connection))EntityManager.SetComponentData(connection,value);
        else EntityManager.AddComponentData(connection,value);
        var request=EntityManager.CreateEntity(typeof(TerrainReadyRequest),typeof(SendRpcCommandRequest));
        EntityManager.SetComponentData(request,new TerrainReadyRequest{Session=header.Session,Epoch=header.Epoch});
        EntityManager.SetComponentData(request,new SendRpcCommandRequest{TargetConnection=connection});
    }
    static void ValidateTheme(TerrainTheme theme,int seed,int tiers,int pixels,float region)
    {
        if(theme.groundVisualSeed!=seed||theme.mountainVisualTiers!=tiers||theme.mountainCaps[0].height!=pixels||theme.groundRegionCells!=region)
            throw new InvalidOperationException("Scene TerrainTheme differs from the grid's baked terrain presentation settings.");
    }
    protected override void OnDestroy()
    {
        using var query=EntityManager.CreateEntityQuery(typeof(MapPresentationBinding));
        using var entities=query.ToEntityArray(Allocator.Temp);
        foreach(var entity in entities)EntityManager.GetComponentObject<MapPresentationBinding>(entity).Release();
    }
}
