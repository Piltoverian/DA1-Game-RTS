using System;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-shot Main smoke test. An external request is consumed only after Unity has imported/compiled it.
[InitializeOnLoad]
public static class BootstrapSpawnSmokeTest
{
    const string Request = "Artifacts/SpawnSmoke/request.txt";
    const string Report = "Artifacts/SpawnSmoke/report.txt";
    const string Active = "BootstrapSpawnSmoke.Active";
    const string Started = "BootstrapSpawnSmoke.Started";
    const string Ready = "BootstrapSpawnSmoke.Ready";
    const string ExitCode = "BootstrapSpawnSmoke.ExitCode";
    static string lastProgress = "waiting for Play Mode/world";

    static BootstrapSpawnSmokeTest() { EditorApplication.update += Tick; }

    [MenuItem("Tools/MapGen/Test Main resource spawn and grid snapper")]
    public static void Run()
    {
        Directory.CreateDirectory("Artifacts/SpawnSmoke");
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Run the test from Edit Mode after compilation.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
            { File.WriteAllText(Report, "BLOCKED: unsaved scene changes; save before running the Main smoke test."); return; }
        MathChecks();
        ResourcePlannerChecks();
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Main.unity");
        SessionState.SetBool(Active, true);
        SessionState.SetString(Started, DateTime.UtcNow.ToString("O"));
        SessionState.SetString(Ready, "");
        SessionState.SetInt(ExitCode, -1);
        File.WriteAllText(Report, "RUNNING: Main resource spawn, grid footprint, population and one-shot checks.");
        EditorApplication.isPlaying = true;
    }

    static void MathChecks()
    {
        foreach (float cellSize in new[] { .25f, 1f, 1.953125f })
        foreach (int size in new[] { 1, 2, 7, 8 })
        {
            var grid = new GridComponent { width = 128, height = 128, cellsize = cellSize, origin = new float3(-19, 0, -31) };
            var center = GridSnapperMath.Snap(GridHelper.GridToWorld(new int2(40, 40), grid), grid, size);
            if (math.distance(center, GridSnapperMath.Snap(center, grid, size)) > .0001f)
                throw new Exception("Grid snapping must be idempotent for even and odd sizes.");
            var rect = new StartEndRect(new float2(-size * cellSize * .5f));
            rect.ExpandTo(new float2(size * cellSize * .5f));
            if (!PlayerSpawnPlacement.TryFootprint(grid, center, rect, out var min, out var max) ||
                math.any(max - min + 1 != new int2(size))) throw new Exception("Grid footprint parity/cell-size check failed.");
            var scale = GridSnapperMath.Scale(new float3(18.9f, 17.9f, 16.3f), size, cellSize);
            if (math.abs(scale.x * 18.9f - size * cellSize) > .0001f || math.abs(scale.z * 16.3f - size * cellSize) > .0001f)
                throw new Exception("Grid resize square check failed.");
        }
    }

    static void Tick()
    {
        const string plannerRequest = "Artifacts/SpawnSmoke/resource-request.txt";
        if (File.Exists(plannerRequest) && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
        {
            File.Delete(plannerRequest);
            TestResourcePlanner();
        }
        int pendingExit = SessionState.GetInt(ExitCode, -1);
        if (Application.isBatchMode && pendingExit >= 0 && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            SessionState.SetInt(ExitCode, -1);
            EditorApplication.Exit(pendingExit);
            return;
        }
        if (!SessionState.GetBool(Active, false))
        {
            if (!Application.isBatchMode && File.Exists(Request) && !EditorApplication.isPlayingOrWillChangePlaymode &&
                !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                File.Delete(Request);
                try { Run(); } catch (Exception e) { File.WriteAllText(Report, "FAIL: " + e); }
            }
            return;
        }
        try
        {
            double elapsed = (DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Started, ""), null,
                System.Globalization.DateTimeStyles.RoundtripKind)).TotalSeconds;
            if (elapsed > 90) { Finish("FAIL: Main did not reach Ready within 90 seconds; " + lastProgress); return; }
            if (!EditorApplication.isPlaying) return;
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;
            var em = world.EntityManager;
            using var roots = em.CreateEntityQuery(typeof(PlayerBootstrapState));
            lastProgress = $"world={world.Name}; bootstrap roots={roots.CalculateEntityCount()}; paused={EditorApplication.isPaused}";
            if (roots.IsEmpty) return;
            var root = roots.GetSingletonEntity();
            var state = em.GetComponentData<PlayerBootstrapState>(root);
            lastProgress += $"; phase={state.Phase}";
            using (var waitingGrids = em.CreateEntityQuery(typeof(GridComponent)))
            {
                lastProgress += $"; grids={waitingGrids.CalculateEntityCount()}";
                if (waitingGrids.CalculateEntityCount() == 1)
                {
                    var waitingGrid = waitingGrids.GetSingleton<GridComponent>();
                    lastProgress += $"; generation={waitingGrid.generation}; islandGeneration={waitingGrid.islandGeneration}; dirty={waitingGrid.isDirty}";
                }
            }
            if (state.Phase == PlayerBootstrapPhase.Failed) { Finish("FAIL: bootstrap rejected the full starting layout; inspect Unity Console."); return; }
            if (state.Phase != PlayerBootstrapPhase.Ready) return;
            using var grids = em.CreateEntityQuery(typeof(GridComponent));
            var ge = grids.GetSingletonEntity();
            var grid = em.GetComponentData<GridComponent>(ge);
            using var spawned = em.GetBuffer<PlayerBootstrapSpawned>(root).ToNativeArray(Allocator.Temp);
            int houses = 0, workers = 0, nodes = 0;
            var types = new int[3];
            foreach (var entry in spawned)
            {
                Entity entity = entry.Value;
                if (em.HasComponent<UnitComponent>(entity)) workers++;
                if (em.HasComponent<BuildingComponent>(entity)) houses++;
                if (em.HasComponent<ResourceNodeData>(entity)) { nodes++; types[(int)em.GetComponentData<ResourceNodeData>(entity).Type]++; }
                if (em.HasComponent<ResourceNodeData>(entity))
                {
                    if (em.HasComponent<ResourceNodePendingConfig>(entity) || em.GetComponentData<ResourceNodeData>(entity).Amount <= 0)
                        throw new Exception("Spawned mine did not receive configured stock before becoming live.");
                    if (em.HasComponent<MapResourceIdentity>(entity))
                    {
                        var id = em.GetComponentData<MapResourceIdentity>(entity).DefinitionId;
                        bool matched = false;
                        foreach (var definition in em.GetBuffer<MapResourcePrefab>(root))
                        {
                            if (definition.Id != id) continue;
                            matched = true;
                            if (em.GetComponentData<ResourceNodeData>(entity).Amount != definition.AmountPerMine)
                                throw new Exception("Spawned stock does not match resource catalog.");
                            if (em.GetComponentData<ResourceNodeData>(definition.Value).Amount != 0)
                                throw new Exception("Prefab template must not contain live mine stock.");
                        }
                        if (!matched) throw new Exception("Spawned resource ID is missing from catalog.");
                    }
                }
                if (!em.HasComponent<GridFootprint>(entity)) continue;
                var footprint = em.GetComponentData<GridFootprint>(entity);
                if (!PlayerSpawnPlacement.TryFootprint(grid, em.GetComponentData<LocalTransform>(entity).Position,
                    em.GetComponentData<BlockageData>(entity).LocalRect, out var min, out var max) ||
                    math.any(max - min + 1 != new int2(footprint.Cells))) throw new Exception("Actual baked footprint has an extra/missing row or column.");
                if (em.HasComponent<BuildingComponent>(entity) && footprint.Cells != 7) throw new Exception("Expected 7x7 test town hall.");
                if (em.HasComponent<ResourceNodeData>(entity) && footprint.Cells != 1) throw new Exception("Expected 1x1 test resource.");
                if (!em.HasComponent<PhysicsCollider>(entity)) throw new Exception("Snapped prefab must have a physical collider.");
                var physics = em.GetComponentData<PhysicsCollider>(entity);
                var bounds = physics.Value.Value.CalculateAabb();
                float side = footprint.Cells * grid.cellsize;
                if (math.abs(bounds.Max.x - bounds.Min.x - side) > .001f || math.abs(bounds.Max.z - bounds.Min.z - side) > .001f ||
                    math.abs(bounds.Max.x + bounds.Min.x) > .001f || math.abs(bounds.Max.z + bounds.Min.z) > .001f)
                    throw new Exception("Physical collider does not match the square centered grid footprint.");
                var costs = em.GetBuffer<GridNodeCost>(ge);
                for (int y = min.y; y <= max.y; y++) for (int x = min.x; x <= max.x; x++)
                    if (costs[GridHelper.GetNodeIndex(new int2(x, y), grid)].cost != 255) throw new Exception("Spawned footprint is not blocked.");
            }
            int expectedNodes = 0;
            string resourceSummary = "0 map resource nodes (resource generation disabled/unconfigured)";
            if (em.HasComponent<MapResourceReport>(root))
            {
                var report = em.GetComponentData<MapResourceReport>(root);
                var config = em.GetComponentData<MapResourceSettings>(root);
                expectedNodes = report.Nodes;
                if (report.RequestedClusters != 4 * math.csum(config.Quotas) ||
                    report.PlacedClusters > report.RequestedClusters ||
                    report.MainPlacedClusters != 4 * config.Quotas.x ||
                    report.MainNodes != report.MainPlacedClusters * config.MainMineCount ||
                    report.Nodes < report.MainNodes + (report.PlacedClusters - report.MainPlacedClusters) * config.ClusterMin ||
                    report.Nodes > report.MainNodes + (report.PlacedClusters - report.MainPlacedClusters) * config.ClusterMax ||
                    report.RequestedClusters > 0 && report.PlacedClusters == 0)
                    throw new Exception("Invalid/empty map resource cluster report.");
                var islands = em.GetBuffer<GridIsland>(ge);
                int island = 0;
                for (int i = 0; i < islands.Length; i++)
                {
                    if (islands[i].islandID == 0) continue;
                    if (island != 0 && island != islands[i].islandID) throw new Exception("Resource occupancy split navigation into multiple islands.");
                    island = islands[i].islandID;
                }
                resourceSummary = $"{report.PlacedClusters}/{report.RequestedClusters} clusters; {report.Nodes} Crystal/Gold nodes";
            }
            if (houses != 4 || workers != 20 || nodes != expectedNodes || types[0] != expectedNodes || types[1] != 0 || types[2] != 0)
                throw new Exception($"Unexpected spawn counts: houses={houses}, workers={workers}, nodes={nodes}.");
            foreach (var slot in em.GetBuffer<PlayerBootstrapSlot>(root))
            {
                var context = em.GetComponentData<PlayerContext>(slot.Context);
                if (context.currentPopulation != 5 || context.maxPopulation != 20) throw new Exception("Per-player population mismatch.");
            }
            string ready = SessionState.GetString(Ready, "");
            if (ready == "")
            {
                SessionState.SetString(Ready, DateTime.UtcNow.ToString("O"));
                SessionState.SetString("BootstrapSpawnSmoke.Generation", grid.generation.ToString());
                var cell = new int2(0, 0);
                var center = GridHelper.GridToWorld(cell, grid);
                var area = new StartEndRect(new float2(center.x, center.z) - grid.cellsize * .5f);
                area.ExpandTo(new float2(center.x, center.z) + grid.cellsize * .5f);
                em.GetBuffer<CostChangeRequest>(ge).Add(new CostChangeRequest {
                    area = area, newCost = em.GetBuffer<GridNodeCost>(ge)[0].cost });
                return;
            }
            if (grid.generation.ToString() != SessionState.GetString("BootstrapSpawnSmoke.Generation", "") ||
                grid.islandGeneration != grid.generation)
                throw new Exception("Idle/no-op cost request changed the grid/island version.");
            if ((DateTime.UtcNow - DateTime.Parse(ready, null, System.Globalization.DateTimeStyles.RoundtripKind)).TotalSeconds < 3) return;
            Finish($"PASS: 12 cell-size/parity math cases; Main Ready; 4 square 7x7 houses, 20 workers, {resourceSummary}, square 1x1 (no Wood/Food nodes); physical colliders match centered footprints; costs blocked; population 5/20 per player; counts and grid/island versions stable for 3 seconds including a no-op cost request.");
        }
        catch (Exception e) { Finish("FAIL: " + e); }
    }

    [MenuItem("Tools/MapGen/Test resource planner only")]
    public static void TestResourcePlanner()
    {
        Directory.CreateDirectory("Artifacts/SpawnSmoke");
        try { ResourcePlannerChecks(); }
        catch (Exception error) { File.WriteAllText("Artifacts/SpawnSmoke/resource-planner.txt", "FAIL: " + error); Debug.LogException(error); }
    }

    static void ResourcePlannerChecks()
    {
        using var world = new World("Resource planner isolated check");
        var em = world.EntityManager;
        var prefab = em.CreateEntity(typeof(Prefab), typeof(ResourceNodeData), typeof(ResourceNodeTag),
            typeof(GridFootprint), typeof(BlockageData), typeof(LocalTransform));
        em.SetComponentData(prefab, new ResourceNodeData { Type = ResourceType.Gold, Amount = 0 });
        em.SetComponentData(prefab, new GridFootprint { Cells = 1, WorldSize = 1 });
        var rect = new StartEndRect(new float2(-.5f)); rect.ExpandTo(new float2(.5f));
        em.SetComponentData(prefab, new BlockageData { LocalRect = rect, CustomCost = 255 });
        em.SetComponentData(prefab, LocalTransform.Identity);
        var root = em.CreateEntity(); var prefabs = em.AddBuffer<MapResourcePrefab>(root);
        prefabs.Add(new MapResourcePrefab { Value = prefab, Id = new FixedString64Bytes("gold.common"), Type = ResourceType.Gold, AllowedClusters = ResourceClusterMask.All, Weight = 1, AmountPerMine = 1500 });
        prefabs.Add(new MapResourcePrefab { Value = prefab, Id = new FixedString64Bytes("gold.common.b"), Type = ResourceType.Gold,
            AllowedClusters = ResourceClusterMask.All, Weight = 2, AmountPerMine = 1200 });
        prefabs.Add(new MapResourcePrefab { Value = prefab, Id = new FixedString64Bytes("gold.rich"), Type = ResourceType.Gold,
            AllowedClusters = ResourceClusterMask.All, Weight = 1, Tier = ResourceValueTier.Rich, AmountPerMine = 3000 });
        prefabs.Add(new MapResourcePrefab { Value = prefab, Id = new FixedString64Bytes("gold.premium"), Type = ResourceType.Gold,
            AllowedClusters = ResourceClusterMask.All, Weight = 1, Tier = ResourceValueTier.Premium, AmountPerMine = 6000 });
        var grid = new GridComponent { width = 128, height = 128, cellsize = 1 };
        using var terrainStorage = new NativeArray<GridTerrain>(128 * 128, Allocator.Temp);
        var terrain = terrainStorage;
        using var costStorage = new NativeArray<GridNodeCost>(terrain.Length, Allocator.Temp);
        var costs = costStorage;
        using var occupied = new NativeArray<bool>(terrain.Length, Allocator.Temp);
        for (int i = 0; i < terrain.Length; i++) { terrain[i] = new GridTerrain { walkable = true, RampId = -1 }; costs[i] = new GridNodeCost { cost = 1 }; }
        var config = new MapResourceSettings { Seed = 30000, ClusterMin = 2, ClusterMax = 4, Quotas = new int4(1),
            MainDistanceCells = 12, MainMineCount = 3,
            SecondaryRange = new float2(.11f,.22f), AdvantageRange = new float2(.23f,.4f), ContestedRange = new float2(.07f,.28f),
            MinimumClusterRadius = 4, RadiusPerSqrtCount = 2.3f, ClusterGap = 3, MemberSpacing = 3,
            CenterAttempts = 100, MemberAttempts = 160 };
        var bases = new[] { new int2(24,24), new int2(103,24), new int2(103,103), new int2(24,103) };
        var first = MapResourcePlanner.Plan(em, grid, terrain, costs, occupied, bases, config, prefabs, out var report);
        if (report.RequestedClusters != 16 || report.PlacedClusters == 0 || first.Count < report.PlacedClusters * 2 || first.Count > report.PlacedClusters * 4)
            throw new Exception("Cluster quota/count checks failed on flat fixture.");
        var mainCounts = new int[bases.Length];
        foreach (var node in first)
        {
            if (node.Role != 0) continue;
            mainCounts[node.PlayerSlot]++;
            if (math.abs(math.distance(node.ClusterCenter, bases[node.PlayerSlot]) - config.MainDistanceCells) > .001f)
                throw new Exception("Main cluster distance differs between players.");
        }
        foreach (int count in mainCounts) if (count != config.MainMineCount * config.Quotas.x)
            throw new Exception("Main mine count differs between players.");
        foreach (var node in first)
        {
            var expectedTier = node.Role == 3 ? ResourceValueTier.Premium : node.Role == 2 ? ResourceValueTier.Rich : ResourceValueTier.Common;
            if (node.Tier != expectedTier) throw new Exception("Resource tier leaked into the wrong cluster role.");
            if (node.Tier == ResourceValueTier.Rich && node.AmountPerMine != 3000 ||
                node.Tier == ResourceValueTier.Premium && node.AmountPerMine != 6000)
                throw new Exception("Catalog stock mapping was lost while planning.");
        }
        for (int p = 1; p < bases.Length; p++)
        {
            var reference = first.FindAll(n => n.Role == 0 && n.PlayerSlot == 0);
            var player = first.FindAll(n => n.Role == 0 && n.PlayerSlot == p);
            for (int i = 0; i < reference.Count; i++)
                if (reference[i].DefinitionId != player[i].DefinitionId || reference[i].AmountPerMine != player[i].AmountPerMine)
                    throw new Exception("Main resource recipe differs between players.");
        }
        int blocked = 0; foreach (var cost in costs) if (cost.cost == 255) blocked++;
        if (blocked != first.Count) throw new Exception("Cluster radius must not block empty terrain.");
        for (int i = 0; i < costs.Length; i++) costs[i] = new GridNodeCost { cost = 1 };
        var second = MapResourcePlanner.Plan(em, grid, terrain, costs, occupied, bases, config, prefabs, out var repeat);
        if (second.Count != first.Count || repeat.PlacedClusters != report.PlacedClusters) throw new Exception("Cluster seed is not deterministic.");
        for (int i = 0; i < first.Count; i++) if (math.any(first[i].Position != second[i].Position)) throw new Exception("Cluster positions are not deterministic.");
        for (int i = 0; i < costs.Length; i++) { costs[i] = new GridNodeCost { cost = 255 }; terrain[i] = new GridTerrain { isMountain = true }; }
        bool mainRejected = false;
        try { MapResourcePlanner.Plan(em, grid, terrain, costs, occupied, bases, config, prefabs, out _); }
        catch (InvalidOperationException) { mainRejected = true; }
        if (!mainRejected) throw new Exception("Missing main package must reject the complete plan.");
        foreach (var cost in costs) if (cost.cost != 255) throw new Exception("Rejected main plan changed costs.");
        config.Quotas.x = 0; // Optional clusters can still report missing quota.
        var rejected = MapResourcePlanner.Plan(em, grid, terrain, costs, occupied, bases, config, prefabs, out var missing);
        if (rejected.Count != 0 || missing.PlacedClusters != 0 || missing.RequestedClusters != 12)
            throw new Exception("Blocked terrain must reject complete clusters and report missing quota.");
        var invalidStock = prefabs[0]; invalidStock.AmountPerMine = 0; prefabs[0] = invalidStock;
        bool stockRejected = false;
        try { MapResourcePlanner.Plan(em, grid, terrain, costs, occupied, bases, config, prefabs, out _); }
        catch (InvalidOperationException) { stockRejected = true; }
        if (!stockRejected) throw new Exception("Zero config stock must fail, not fall back to prefab stock.");
        File.WriteAllText("Artifacts/SpawnSmoke/resource-planner.txt", $"PASS: flat fixture {report.PlacedClusters}/16 clusters, {report.Nodes} nodes; seed determinism; fixed main distance, mine count and recipe per player; tier/stock catalog mapping from zero-stock templates; zero config stock rejected; footprint-only occupancy; unavailable main rejects full plan; blocked optional clusters report missing quota.");
    }

    static void Finish(string result)
    {
        SessionState.SetBool(Active, false);
        File.WriteAllText(Report, result);
        Debug.Log(result);
        if (Application.isBatchMode) SessionState.SetInt(ExitCode, result.StartsWith("PASS") ? 0 : 1);
        EditorApplication.isPlaying = false;
    }
}








