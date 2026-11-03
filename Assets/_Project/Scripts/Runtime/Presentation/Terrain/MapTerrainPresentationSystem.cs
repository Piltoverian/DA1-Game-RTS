using System;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

public enum MapPresentationPhase : byte { WaitingTerrain, Ready, Failed, Building }
public struct MapPresentationState : IComponentData
{
    public MapPresentationPhase Phase;
    public uint Revision; // Player-view geometry version, not authoritative map revision.
    public FixedString512Bytes Error;
}

public sealed class MapPresentationBinding : IComponentData
{
    public TerrainMapRenderer Owner;
    public LocalBaseCameraSnapper Snapper;
    public TerrainChunkRenderer Renderer;
    public readonly LocalTerrainView Local = new();
    public TerrainView View;
    public TerrainChunkRenderer Candidate;
    public System.Collections.IEnumerator BuildSteps;
    public uint CandidateVersion;
    public double RetryAfter;
    public void CancelBuild()
    {
        (BuildSteps as IDisposable)?.Dispose(); BuildSteps=null;
        if (Candidate)
        {
            Candidate.Clear();
            if(Application.isPlaying) UnityEngine.Object.Destroy(Candidate); else UnityEngine.Object.DestroyImmediate(Candidate);
        }
        Candidate=null;
    }
    public void Release()
    {
        if (Renderer)
        {
            Renderer.Clear();
            if (Application.isPlaying) UnityEngine.Object.Destroy(Renderer); else UnityEngine.Object.DestroyImmediate(Renderer);
        }
        Renderer = null;
    }
    public void Dispose() { CancelBuild(); Release(); Local.Dispose(); View = null; }
}

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
[UpdateAfter(typeof(TerrainNetworkReceiveSystem))]
public partial class MapTerrainPresentationSystem : SystemBase
{
    protected override void OnCreate() { RequireForUpdate<MapPresentationBinding>(); }
    protected override void OnUpdate()
    {
        bool network = World.IsClient();
        var receiver = network ? World.GetExistingSystemManaged<TerrainNetworkReceiveSystem>() : null;
        var fog = network ? null : World.GetExistingSystemManaged<LocalFogPresentationSystem>();
        using var query = EntityManager.CreateEntityQuery(typeof(MapPresentationBinding), typeof(MapPresentationState));
        using var entities = query.ToEntityArray(Allocator.Temp);
        foreach (var entity in entities)
        {
            var binding = EntityManager.GetComponentObject<MapPresentationBinding>(entity);
            if (!binding.Owner || !binding.Owner.isActiveAndEnabled) continue;
            var view = network ? receiver?.View
                : binding.Local.Update(EntityManager, fog) ? binding.Local.View : null;
            binding.View = view;
            var status = EntityManager.GetComponentData<MapPresentationState>(entity);
            if (view == null || !view.Ready)
            {
                binding.CancelBuild();
                // No fresh observation is not a request to destroy the last authorized image.
                if(!binding.Renderer) EntityManager.SetComponentData(entity, new MapPresentationState { Phase = MapPresentationPhase.WaitingTerrain });
                continue;
            }
            if (binding.Renderer) binding.Renderer.SetFog(view.Mask);
            if (binding.BuildSteps == null && status.Revision == view.GeometryVersion && binding.Renderer) continue;
            if (UnityEngine.Time.realtimeSinceStartupAsDouble < binding.RetryAfter) continue;
            var camera = binding.Owner.ResolveCamera();
            if (!camera) continue;
            bool firstBuild = !binding.Renderer;
            try
            {
                var theme = binding.Owner.theme;
                if (!theme || !theme.IsRenderReady(out _))
                {
                    // Dependencies may become available in a later loading frame.
                    if(!binding.Renderer) status.Phase=MapPresentationPhase.WaitingTerrain;
                    EntityManager.SetComponentData(entity,status); continue;
                }
                ValidateTheme(theme, view.Settings);
                IsometricTerrainCamera.Configure(camera);
                if(view.Mask){camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;}
                if(binding.BuildSteps==null)
                {
                    binding.CandidateVersion=view.GeometryVersion;
                    binding.Candidate=binding.Owner.gameObject.AddComponent<TerrainChunkRenderer>();
                    binding.BuildSteps=binding.Candidate.BuildInSteps(view.Grid,theme,camera,World,view.Cells,binding.Renderer);
                    if(!binding.Renderer) status.Phase=MapPresentationPhase.Building;
                }
                bool more;
                using(RuntimePerformanceProbe.Measure("terrainBuildStep")) more=binding.BuildSteps.MoveNext();
                if(more){EntityManager.SetComponentData(entity,status);continue;}
                (binding.BuildSteps as IDisposable)?.Dispose(); binding.BuildSteps=null;
                var completed=binding.Candidate; binding.Candidate=null;
                completed.SetFog(view.Mask); completed.Publish();
                binding.Release(); binding.Renderer=completed;
                status=new MapPresentationState { Phase=MapPresentationPhase.Ready, Revision=binding.CandidateVersion };
                binding.RetryAfter=0;
                if(network)
                {
                    if(firstBuild && binding.Snapper && binding.Snapper.snapOnReady) binding.Snapper.Snap(camera,receiver.Header.SpawnPosition);
                    EntityManager.SetComponentEnabled<CameraSnapPending>(entity,false);
                }
                else if(firstBuild) EntityManager.SetComponentEnabled<CameraSnapPending>(entity,true);
            }
            catch(Exception error)
            {
                binding.CancelBuild();
                var message=new FixedString512Bytes(error.Message.Length>400?error.Message.Substring(0,400):error.Message);
                if(!status.Error.Equals(message)) Debug.LogException(error);
                status.Error=message;
                status.Phase=binding.Renderer?MapPresentationPhase.Ready:MapPresentationPhase.Failed;
                binding.RetryAfter=UnityEngine.Time.realtimeSinceStartupAsDouble+1;
            }
            EntityManager.SetComponentData(entity, status);
        }
    }
    static void ValidateTheme(TerrainTheme theme, TerrainSyncSettings settings)
    {
        if (theme.groundVisualSeed != settings.VisualSeed || theme.mountainVisualTiers != settings.MountainTiers
            || theme.mountainCaps[0].height != settings.CapPixels || theme.groundRegionCells != settings.GroundRegionCells)
            throw new InvalidOperationException("Scene TerrainTheme differs from the grid's baked terrain presentation settings.");
    }
    protected override void OnDestroy()
    {
        using var query = EntityManager.CreateEntityQuery(typeof(MapPresentationBinding));
        using var entities = query.ToEntityArray(Allocator.Temp);
        foreach (var entity in entities) EntityManager.GetComponentObject<MapPresentationBinding>(entity).Dispose();
    }
}
