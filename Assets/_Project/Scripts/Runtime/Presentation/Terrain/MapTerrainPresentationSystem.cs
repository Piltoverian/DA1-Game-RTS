using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
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
    protected override void OnCreate(){RequireForUpdate<GridComponent>();RequireForUpdate<MapPresentationBinding>();}
    protected override void OnUpdate()
    {
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
            if(state.Phase==MapPresentationPhase.Failed&&state.Revision==revision)continue;
            if(state.Phase==MapPresentationPhase.Ready&&state.Revision==revision&&binding.Renderer
                &&(fog==null||(binding.FogScope==fog.ScopeVersion&&binding.KnownCells==fog.View.Terrain.Cells.Count)))continue;
            var camera=Camera.main;if(!camera)continue;
            bool firstBuild=state.Phase!=MapPresentationPhase.Ready;
            try{
                using var probe=RuntimePerformanceProbe.Measure("terrainCompileAndBuild");
                if(!binding.Owner.theme)throw new InvalidOperationException("Assign TerrainMapRenderer.theme");
                binding.Release();
                var map=new GeneratedTerrain{grid=grid,cells=new GridTerrain[cells.Length],spawns=Array.Empty<int2>()};
                if(fog==null){for(int i=0;i<cells.Length;i++)map.cells[i]=cells[i];}
                else {
                    // Placeholder cells carry only the shared height datum; their
                    // actual terrain remains unknown and no sprites are emitted.
                    for(int i=0;i<map.cells.Length;i++)map.cells[i].heightLevel=fog.Frame.TerrainBaseLevel;
                    foreach(var cell in fog.View.Terrain.Cells)map.cells[cell.Key]=cell.Value;
                }
                if(EntityManager.HasBuffer<GridSpawnCell>(ge)){
                    var anchors=EntityManager.GetBuffer<GridSpawnCell>(ge);map.spawns=new int2[anchors.Length];
                    for(int i=0;i<anchors.Length;i++)map.spawns[i]=anchors[i].cell;
                }
                TerrainRampCompiler.Compile(map);IsometricTerrainCamera.Configure(camera);
                binding.Renderer=binding.Owner.gameObject.AddComponent<TerrainChunkRenderer>();
                binding.Renderer.Build(map,binding.Owner.theme,camera,World,fog?.View.Terrain.Cells);
                if(fog!=null){binding.Renderer.SetFog(fog.Mask);binding.FogScope=fog.ScopeVersion;binding.KnownCells=fog.View.Terrain.Cells.Count;}
                binding.Map=map;
                state=new MapPresentationState{Phase=MapPresentationPhase.Ready,Revision=revision};
                if(firstBuild)EntityManager.SetComponentEnabled<CameraSnapPending>(entity,true);
            }catch(Exception error){binding.Release();state=new MapPresentationState{Phase=MapPresentationPhase.Failed,Revision=revision,Error=new FixedString512Bytes(error.Message.Length>400?error.Message.Substring(0,400):error.Message)};Debug.LogException(error);}
            EntityManager.SetComponentData(entity,state);
        }
    }
    protected override void OnDestroy()
    {
        using var query=EntityManager.CreateEntityQuery(typeof(MapPresentationBinding));
        using var entities=query.ToEntityArray(Allocator.Temp);
        foreach(var entity in entities)EntityManager.GetComponentObject<MapPresentationBinding>(entity).Release();
    }
}
