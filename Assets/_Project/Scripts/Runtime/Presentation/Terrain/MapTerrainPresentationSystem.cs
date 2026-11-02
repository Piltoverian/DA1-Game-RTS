using System;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

public enum MapPresentationPhase : byte { WaitingTerrain, Ready, Failed }
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
    public void Release()
    {
        if (Renderer)
        {
            Renderer.Clear();
            if (Application.isPlaying) UnityEngine.Object.Destroy(Renderer); else UnityEngine.Object.DestroyImmediate(Renderer);
        }
        Renderer = null;
    }
    public void Dispose() { Release(); Local.Dispose(); View = null; }
}

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
[UpdateAfter(typeof(LocalFogPresentationSystem))]
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
                binding.Release();
                EntityManager.SetComponentData(entity, new MapPresentationState { Phase = MapPresentationPhase.WaitingTerrain });
                continue;
            }
            if (binding.Renderer) binding.Renderer.SetFog(view.Mask);
            if (status.Revision == view.GeometryVersion && (binding.Renderer || status.Phase == MapPresentationPhase.Failed)) continue;
            var camera = binding.Owner.ResolveCamera();
            if (!camera) continue;
            bool firstBuild = status.Phase != MapPresentationPhase.Ready;
            try
            {
                using var probe = RuntimePerformanceProbe.Measure("terrainCompileAndBuild");
                var theme = binding.Owner.theme;
                if (!theme || !theme.IsRenderReady(out _)) throw new InvalidOperationException("Terrain theme is missing or not render ready.");
                ValidateTheme(theme, view.Settings);
                IsometricTerrainCamera.Configure(camera);
                if (view.Mask) { camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; }
                binding.Release();
                binding.Renderer = binding.Owner.gameObject.AddComponent<TerrainChunkRenderer>();
                binding.Renderer.Build(view.Grid, theme, camera, World, view.Cells);
                binding.Renderer.SetFog(view.Mask);
                status = new MapPresentationState { Phase = MapPresentationPhase.Ready, Revision = view.GeometryVersion };
                if (network)
                {
                    if (firstBuild && binding.Snapper && binding.Snapper.snapOnReady) binding.Snapper.Snap(camera, receiver.Header.SpawnPosition);
                    EntityManager.SetComponentEnabled<CameraSnapPending>(entity, false);
                    ReportTerrainReady(receiver.Header);
                }
                else if (firstBuild) EntityManager.SetComponentEnabled<CameraSnapPending>(entity, true);
            }
            catch (Exception error)
            {
                binding.Release();
                status = new MapPresentationState { Phase = MapPresentationPhase.Failed, Revision = view.GeometryVersion,
                    Error = new FixedString512Bytes(error.Message.Length > 400 ? error.Message.Substring(0, 400) : error.Message) };
                Debug.LogException(error);
            }
            EntityManager.SetComponentData(entity, status);
        }
    }
    void ReportTerrainReady(TerrainChunkGhost header)
    {
        using var query = EntityManager.CreateEntityQuery(typeof(NetworkId), typeof(NetworkStreamInGame));
        if (query.CalculateEntityCount() != 1) return;
        var connection = query.GetSingletonEntity();
        if (EntityManager.HasComponent<TerrainClientReady>(connection))
        {
            var previous = EntityManager.GetComponentData<TerrainClientReady>(connection);
            if (previous.Session == header.Session && previous.Epoch == header.Epoch) return;
        }
        var value = new TerrainClientReady { Session = header.Session, Epoch = header.Epoch };
        if (EntityManager.HasComponent<TerrainClientReady>(connection)) EntityManager.SetComponentData(connection, value);
        else EntityManager.AddComponentData(connection, value);
        var request = EntityManager.CreateEntity(typeof(TerrainReadyRequest), typeof(SendRpcCommandRequest));
        EntityManager.SetComponentData(request, new TerrainReadyRequest { Session = header.Session, Epoch = header.Epoch });
        EntityManager.SetComponentData(request, new SendRpcCommandRequest { TargetConnection = connection });
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
