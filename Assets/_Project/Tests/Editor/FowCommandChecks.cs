using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

// Real ECS regression fixtures. Run with -executeMethod FowCommandChecks.Run.
public static class FowCommandChecks
{
    static int failed;
    static readonly List<string> results = new();
    [MenuItem("Tests/FOW Commands")]
    public static void Run()
    {
        failed = 0; results.Clear();
        Check("hidden attack becomes move", HiddenAttack);
        Check("builder finds nearby unfinished site after queue", NearbyBuild);
        Check("preview hides unknown occupancy", PreviewUnknown);
        Check("known terrain rejects build through fog", PreviewTerrain);
        Check("unseen terrain does not leak buildability", PreviewUnseenTerrain);
        Check("whole footprint needed for valid preview", PreviewPartial);
        Check("visible occupancy rejects placement", PreviewBlocked);
        Check("visible free footprint accepts placement", PreviewValid);
        Check("repair visits destroyed snapshot then idles", RepairSnapshot);
        Check("gather visits destroyed snapshot then idles", GatherSnapshot);
        Check("unknown plan waits before creating site", DeferredPlan);
        Check("build failure continues FIFO", QueueAfterFailure);
        Check("repair never consumes build queue", RepairNoQueue);
        Check("automatic build search excludes hidden sites", HiddenNearbyBuild);
        Check("automatic build search respects gather radius", DistantNearbyBuild);
        Check("deferred placement success preserves next plan", PlacementSuccess);
        Check("deferred placement failure starts next plan", PlacementFailure);
        Check("move cancels pending placement", CancelDeferredPlacement);
        Check("unreachable remembered repair stops retrying", FailedRepairTravel);
        Check("snapshot picking respects nearer building", PickingOcclusion);
        foreach (var result in results) Debug.Log(result);
        Debug.Log($"FOW_COMMAND_CHECKS: {results.Count - failed}/{results.Count} passed");
        if (Application.isBatchMode) EditorApplication.Exit(failed == 0 ? 0 : 1);
    }
    static void Check(string name, Action test)
    {
        try { test(); results.Add("PASS " + name); }
        catch (Exception ex) { failed++; results.Add("FAIL " + name + ": " + ex); }
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    sealed class Fixture : IDisposable
    {
        public readonly World World = new("FOW commands regression");
        public EntityManager Em => World.EntityManager;
        public Entity Grid;
        public FogOfWarSpatialSystemData Fog;
        public Fixture()
        {
            Grid = Em.CreateEntity(typeof(GridComponent));
            Em.SetComponentData(Grid, new GridComponent { width = 32, height = 32, cellsize = 1, initialized = true });
            Em.AddBuffer<GridTerrain>(Grid); Em.AddBuffer<GridNodeCost>(Grid); Em.AddBuffer<GridIsland>(Grid);
            var terrain = Em.GetBuffer<GridTerrain>(Grid); var costs = Em.GetBuffer<GridNodeCost>(Grid);
            var islands = Em.GetBuffer<GridIsland>(Grid);
            for (int i = 0; i < 1024; i++) { terrain.Add(new GridTerrain { walkable = true, RampId = -1 }); costs.Add(new GridNodeCost { cost = 1 }); islands.Add(new GridIsland { islandID = 1 }); }
            Fog = new FogOfWarSpatialSystemData { Visible = new(1024, Allocator.Persistent), Explored = new(1024, Allocator.Persistent) };
            Em.AddComponentData(Em.CreateEntity(), Fog);
            var player = Em.CreateEntity(typeof(PlayerContext));
            Em.SetComponentData(player, new PlayerContext { PlayerId = 0, TeamID = -1 });
            Em.AddBuffer<ResourcePair>(player);
        }
        public void Tick(string type) => World.GetOrCreateSystem(typeof(BuilderSystem).Assembly.GetType(type)).Update(World.Unmanaged);
        public Entity Worker(float3 position)
        {
            var e = Em.CreateEntity(typeof(EntityOwner), typeof(LocalTransform), typeof(MoveOverride), typeof(TargetCache), typeof(BuilderComponent));
            Em.SetComponentData(e, new EntityOwner { PlayerID = 0 });
            Em.SetComponentData(e, LocalTransform.FromPosition(position));
            Em.SetComponentData(e, new BuilderComponent { BuildRange = 1, BuildWorkLoadPerSecond = 1 });
            Em.AddBuffer<BuildOffer>(e).Add(new BuildOffer { DefinitionID = "house" });
            Em.AddBuffer<BuilderQueueElement>(e);
            Em.SetComponentEnabled<BuilderComponent>(e, false); Em.SetComponentEnabled<MoveOverride>(e, false);
            return e;
        }
        public Entity Site(float3 position)
        {
            var e = Em.CreateEntity(typeof(EntityOwner), typeof(LocalTransform), typeof(BuildingComponent), typeof(BuildingConstruction), typeof(EntityHealth), typeof(BlockageData));
            Em.SetComponentData(e, new EntityOwner { PlayerID = 0 }); Em.SetComponentData(e, LocalTransform.FromPosition(position));
            Em.SetComponentData(e, new BuildingComponent { DefinitionID = "house", WorkLoad = 100 });
            Em.SetComponentData(e, new BuildingConstruction { Phase = ConstructionPhase.UnderConstruction, CompletedWork = 20 });
            Em.SetComponentData(e, new EntityHealth { CurrentHP = 20, MaxHP = 100 });
            var rect = new StartEndRect(new float2(-.5f)); rect.ExpandTo(new float2(.5f));
            Em.SetComponentData(e, new BlockageData { LocalRect = rect });
            return e;
        }
        public void Dispose() { World.Dispose(); Fog.Visible.Dispose(); Fog.Explored.Dispose(); }
    }
    static void HiddenAttack()
    {
        using var f = new Fixture(); var em = f.Em;
        var worker = f.Worker(new float3(2, 0, 2));
        em.AddComponent<ShootAttack>(worker); em.AddComponent<Target>(worker);
        var enemy = em.CreateEntity(typeof(EntityOwner), typeof(LocalTransform), typeof(EntityHealth));
        em.SetComponentData(enemy, new EntityOwner { PlayerID = 1 }); em.SetComponentData(enemy, LocalTransform.FromPosition(new float3(20, 0, 20)));
        em.SetComponentData(enemy, new EntityHealth { CurrentHP = 100, MaxHP = 100 });
        f.Tick("CommandQueue");
        CommandDataHelper.AddCommandToQueue(em, 0, worker, CommandType.TargetTo, targetEntity: enemy, position: new Vector3(8, 0, 8));
        f.Tick("CommandQueue");
        Require(em.GetComponentData<Target>(worker).targetEntity == Entity.Null, "hidden target acquired");
        Require(em.IsComponentEnabled<MoveOverride>(worker), "move fallback missing");
        Require(math.all(em.GetComponentData<MoveOverride>(worker).targetPosition == new float3(8, 0, 8)), "move used secret position");
    }
    static void NearbyBuild()
    {
        using var f = new Fixture(); var em = f.Em;
        var worker = f.Worker(new float3(10, 0, 10)); var site = f.Site(new float3(12, 0, 10));
        f.Fog.Visible.Add(10 * 32 + 12, 0);
        em.SetComponentEnabled<BuilderComponent>(worker, true);
        f.Tick("BuilderSystem");
        Require(em.IsComponentEnabled<BuilderComponent>(worker), "builder stopped before searching");
        Require(em.GetComponentData<BuilderComponent>(worker).TargetConstructionSite == site, "nearby site not found");
    }

    static StartEndRect Rect() { var r = new StartEndRect(float2.zero); r.ExpandTo(new float2(2)); return r; }
    static void Show(Fixture f, int x, int y) { f.Fog.Visible.Add(y * 32 + x, 0); f.Fog.Explored.Add(y * 32 + x, 0); }
    static void ShowRect(Fixture f) { Show(f, 10, 10); Show(f, 11, 10); Show(f, 10, 11); Show(f, 11, 11); }
    static BuildingPreviewState Preview(Fixture f) => FogOrderRules.Preview(f.Em, 0, new float3(10, 0, 10), Rect());
    static void PreviewUnknown()
    {
        using var f = new Fixture(); var costs = f.Em.GetBuffer<GridNodeCost>(f.Grid); costs[330] = new GridNodeCost { cost = 255 };
        Require(Preview(f) == BuildingPreviewState.Unknown, "hidden occupancy revealed");
    }
    static void PreviewTerrain()
    {
        using var f = new Fixture(); f.Fog.Explored.Add(330, 0);
        var terrain = f.Em.GetBuffer<GridTerrain>(f.Grid); terrain[330] = new GridTerrain { walkable = false, RampId = -1 };
        Require(Preview(f) == BuildingPreviewState.Invalid, "known terrain ignored");
    }
    static void PreviewUnseenTerrain()
    {
        using var f = new Fixture(); var terrain = f.Em.GetBuffer<GridTerrain>(f.Grid); terrain[330] = new GridTerrain { walkable = false, RampId = -1 };
        Require(Preview(f) == BuildingPreviewState.Unknown, "unseen terrain exposed");
    }
    static void PreviewPartial() { using var f = new Fixture(); Show(f, 10, 10); Require(Preview(f) == BuildingPreviewState.Unknown, "one cell authorized full build preview"); }
    static void PreviewBlocked() { using var f = new Fixture(); ShowRect(f); var costs = f.Em.GetBuffer<GridNodeCost>(f.Grid); costs[330] = new GridNodeCost { cost = 255 }; Require(Preview(f) == BuildingPreviewState.Invalid, "visible blocker ignored"); }
    static void PreviewValid() { using var f = new Fixture(); ShowRect(f); Require(Preview(f) == BuildingPreviewState.Valid, "visible clear site rejected"); }
    static FogKnownTarget Known(FogRenderKind kind) => new() { HasValue = true, Kind = kind, Position = new float3(20, 0, 20), Min = new float2(19.5f), Max = new float2(20.5f) };
    static void RepairSnapshot()
    {
        using var f = new Fixture(); var em = f.Em; var worker = f.Worker(new float3(2, 0, 2));
        var gone = f.Site(new float3(20, 0, 20)); em.DestroyEntity(gone); f.Tick("CommandQueue");
        CommandDataHelper.AddCommandToQueue(em, 0, worker, CommandType.TargetTo, targetEntity: gone, knownTarget: Known(FogRenderKind.Building));
        f.Tick("CommandQueue"); f.Tick("BuilderSystem");
        Require(em.IsComponentEnabled<BuilderComponent>(worker) && em.IsComponentEnabled<MoveOverride>(worker), "repair rejected before visit");
        Require(em.GetComponentData<BuilderComponent>(worker).RepairOnly, "repair entered build loop");
        em.SetComponentData(worker, LocalTransform.FromPosition(new float3(19, 0, 20)));
        f.Tick("BuilderSystem");
        Require(!em.IsComponentEnabled<BuilderComponent>(worker), "repair did not idle after confirming missing site");
    }
    static void GatherSnapshot()
    {
        using var f = new Fixture(); var em = f.Em; var worker = f.Worker(new float3(2, 0, 2));
        em.AddComponent<WorkerTag>(worker); em.AddComponentData(worker, new WorkerGatherData { StopDistanceSq = 1 });
        var gone = em.CreateEntity(typeof(ResourceNodeData)); em.DestroyEntity(gone); f.Tick("CommandQueue");
        CommandDataHelper.AddCommandToQueue(em, 0, worker, CommandType.TargetTo, targetEntity: gone, knownTarget: Known(FogRenderKind.Resource));
        f.Tick("CommandQueue"); f.Tick("WorkerGatherSystem"); f.Tick("FogTargetTravelSystem");
        Require(em.HasComponent<FogTargetTravel>(worker) && em.IsComponentEnabled<MoveOverride>(worker), "gather rejected missing hidden node");
        em.SetComponentData(worker, LocalTransform.FromPosition(new float3(19, 0, 20)));
        f.Tick("FogTargetTravelSystem"); f.Tick("WorkerGatherSystem");
        Require(em.GetComponentData<WorkerGatherData>(worker).State == WorkerGatherState.Idle, "gather did not idle after visit");
    }
    static void DeferredPlan()
    {
        using var f = new Fixture(); var em = f.Em; var worker = f.Worker(new float3(2, 0, 2));
        var prefab = f.Site(new float3(20, 0, 20)); em.AddComponent<Prefab>(prefab);
        f.Tick("CommandQueue");
        CommandDataHelper.AddPlanBuildToQueue(em, 0, new[] { worker }, prefab, new Vector3(20, 0, 20));
        f.Tick("CommandQueue"); f.Tick("BuilderSystem");
        using var requests = em.CreateEntityQuery(typeof(PlaceBuildingRequest));
        Require(requests.CalculateEntityCount() == 0, "unknown plan validated/published immediately");
        Require(em.GetComponentData<BuilderComponent>(worker).PlannedPrefab == prefab, "unknown plan lost");
        Show(f, 19, 19); Show(f, 20, 19); Show(f, 19, 20); Show(f, 20, 20);
        f.Tick("BuilderSystem");
        Require(requests.CalculateEntityCount() == 1, "revealed plan not submitted for real placement");
        var builder = em.GetComponentData<BuilderComponent>(worker);
        Require(builder.WaitingForPlacement && em.Exists(builder.PlacementRequest), "deferred request reference was not remapped");
    }
    static void QueueAfterFailure()
    {
        using var f = new Fixture(); var em = f.Em; var worker = f.Worker(new float3(10, 0, 10)); var site = f.Site(new float3(12, 0, 10));
        Show(f, 12, 10);
        em.SetComponentData(worker, new BuilderComponent { BuildRange = 1, State = BuilderState.GoingToSite });
        em.SetComponentEnabled<BuilderComponent>(worker, true);
        em.GetBuffer<BuilderQueueElement>(worker).Add(new BuilderQueueElement { BuildingEntity = site });
        f.Tick("BuilderSystem");
        Require(em.GetComponentData<BuilderComponent>(worker).TargetConstructionSite == site && em.GetBuffer<BuilderQueueElement>(worker).Length == 0, "failed item did not advance FIFO");
    }
    static void RepairNoQueue()
    {
        using var f = new Fixture(); var em = f.Em; var worker = f.Worker(new float3(10, 0, 10)); var site = f.Site(new float3(12, 0, 10)); Show(f, 12, 10);
        em.SetComponentData(worker, new BuilderComponent { RepairOnly = true, State = BuilderState.GoingToSite });
        em.SetComponentEnabled<BuilderComponent>(worker, true);
        em.GetBuffer<BuilderQueueElement>(worker).Add(new BuilderQueueElement { BuildingEntity = site });
        f.Tick("BuilderSystem");
        Require(!em.IsComponentEnabled<BuilderComponent>(worker), "repair chained to unrelated site");
    }
    static void HiddenNearbyBuild()
    {
        using var f = new Fixture(); var worker = f.Worker(new float3(10, 0, 10)); f.Site(new float3(12, 0, 10));
        f.Em.SetComponentEnabled<BuilderComponent>(worker, true); f.Tick("BuilderSystem");
        Require(!f.Em.IsComponentEnabled<BuilderComponent>(worker), "auto search discovered hidden site");
    }
    static void DistantNearbyBuild()
    {
        using var f = new Fixture(); var worker = f.Worker(new float3(10, 0, 10)); f.Site(new float3(16, 0, 10)); Show(f, 16, 10);
        f.Em.SetComponentEnabled<BuilderComponent>(worker, true); f.Tick("BuilderSystem");
        Require(!f.Em.IsComponentEnabled<BuilderComponent>(worker), "auto search exceeded gather window");
    }
    static Entity Prefab(Fixture f)
    {
        var e = f.Site(new float3(20, 0, 20));
        f.Em.RemoveComponent<BuildingConstruction>(e); f.Em.AddComponent<Prefab>(e); f.Em.AddBuffer<EntityResourceCost>(e);
        return e;
    }
    static Entity QueueTwoPlans(Fixture f, out Entity prefab)
    {
        var worker = f.Worker(new float3(2, 0, 2)); prefab = Prefab(f); f.Tick("CommandQueue");
        CommandDataHelper.AddPlanBuildToQueue(f.Em, 0, new[] { worker }, prefab, new Vector3(20, 0, 20)); f.Tick("CommandQueue");
        CommandDataHelper.AddPlanBuildToQueue(f.Em, 0, new[] { worker }, prefab, new Vector3(25, 0, 25)); f.Tick("CommandQueue");
        Show(f, 19, 19); Show(f, 20, 19); Show(f, 19, 20); Show(f, 20, 20);
        f.Tick("BuilderSystem"); return worker;
    }
    static void PlacementSuccess()
    {
        using var f = new Fixture(); var worker = QueueTwoPlans(f, out _);
        f.Tick("BuildingPlacementSystem"); f.Tick("CommandQueue");
        var builder = f.Em.GetComponentData<BuilderComponent>(worker);
        Require(!builder.WaitingForPlacement && f.Em.Exists(builder.TargetConstructionSite), "placement did not resume builder");
        Require(f.Em.GetBuffer<BuilderQueueElement>(worker).Length == 1, "placement erased remaining FIFO");
        Require(f.Em.GetComponentData<LocalTransform>(builder.TargetConstructionSite).Position.x == 20, "wrong plan instantiated");
    }
    static void PlacementFailure()
    {
        using var f = new Fixture(); var worker = QueueTwoPlans(f, out var prefab);
        var costs = f.Em.GetBuffer<GridNodeCost>(f.Grid); costs[19 * 32 + 19] = new GridNodeCost { cost = 255 };
        f.Tick("BuildingPlacementSystem"); f.Tick("CommandQueue"); f.Tick("BuilderSystem");
        var builder = f.Em.GetComponentData<BuilderComponent>(worker);
        Require(builder.PlannedPrefab == prefab && builder.PlannedPosition.x == 25 && !builder.WaitingForPlacement, "failed placement did not advance to next plan");
    }
    static void CancelDeferredPlacement()
    {
        using var f = new Fixture(); var worker = QueueTwoPlans(f, out _);
        var request = f.Em.GetComponentData<BuilderComponent>(worker).PlacementRequest;
        CommandDataHelper.AddCommandToQueue(f.Em, 0, worker, CommandType.Move, position: new Vector3(4, 0, 4)); f.Tick("CommandQueue");
        Require(!f.Em.Exists(request), "cancelled placement still published");
        Require(!f.Em.IsComponentEnabled<BuilderComponent>(worker) && f.Em.GetBuffer<BuilderQueueElement>(worker).Length == 0, "move did not interrupt builder");
    }
    static void FailedRepairTravel()
    {
        using var f = new Fixture(); var worker = f.Worker(new float3(2, 0, 2)); f.Tick("CommandQueue");
        CommandDataHelper.AddCommandToQueue(f.Em, 0, worker, CommandType.TargetTo, targetEntity: Entity.Null, knownTarget: Known(FogRenderKind.Building));
        f.Tick("CommandQueue"); f.Tick("BuilderSystem");
        f.Em.SetComponentEnabled<MoveOverride>(worker, false); // Movement backend reports failed/completed attempt, still outside work range.
        f.Tick("BuilderSystem");
        Require(!f.Em.IsComponentEnabled<BuilderComponent>(worker), "unreachable repair reissued forever");
    }
    static void PickingOcclusion()
    {
        using var f = new Fixture();
        var fog = f.World.GetOrCreateSystemManaged<LocalFogPresentationSystem>();
        var frame = fog.Frame;
        frame.IsValid = true; frame.PlayerId = 0; frame.Width = frame.Height = 32; frame.CellSize = 1;
        var building = new FogEntityRenderData { Entity = f.Site(new float3(10, 2, 10)), Kind = FogRenderKind.Building,
            IsStatic = true, Transform = LocalTransform.FromPosition(new float3(10, 2, 10)), FootprintMin = new int2(10), FootprintMax = new int2(10) };
        var resource = new FogEntityRenderData { Entity = f.Em.CreateEntity(), Kind = FogRenderKind.Resource,
            IsStatic = true, Transform = LocalTransform.FromPosition(new float3(10, 0, 10)), FootprintMin = new int2(10), FootprintMax = new int2(10) };
        frame.Entities.Add(building); frame.Entities.Add(resource); fog.View.Apply(frame);
        var ray = new Ray(new Vector3(10.5f, 10, 10.5f), Vector3.down);
        Require(!FogCommandPicking.TryPick(fog, ray, FogRenderKind.Resource, out _, out _), "gather picked resource through nearer building");
    }
}
