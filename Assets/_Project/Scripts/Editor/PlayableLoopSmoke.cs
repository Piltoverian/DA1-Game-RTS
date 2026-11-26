using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

/// <summary>Opt-in actual Main playthrough. No free resources, instant construction, spawns or damage injection.</summary>
[InitializeOnLoad]
public static class PlayableLoopSmoke
{
    private const string Key = "RTS.PlayableLoopSmoke", Folder = "Temp/PlayableLoop";
    private static bool running;
    private static int phase;
    private static double began, phaseStart, nextTick;
    private static World initialWorld;
    private static Entity home, enemyHome, barracks, depot, tower, gold;
    private static float initialGold, initialNode, elapsed, movedFrom;
    private static float3 moveStart;
    private static readonly List<string> checks = new(), errors = new();
    private static readonly List<float> frames = new();
    private static readonly HashSet<Entity> shots = new();
    private static bool raidProbeSent;
    private static int lastFrame = -1;
    static PlayableLoopSmoke()
    {
        if (File.Exists(Folder + "/cancel-smoke.flag")) { File.Delete(Folder + "/cancel-smoke.flag"); SessionState.SetBool(Key, false); }
        running = SessionState.GetBool(Key, false); began = phaseStart = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
    }
    [MenuItem("Tools/RTS UI/Run complete playable loop smoke")]
    public static void Begin()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first; smoke must start from a clean match.");
        SessionState.SetBool(Key, true); running = true; phase = 0;
        checks.Clear(); errors.Clear(); frames.Clear(); shots.Clear(); raidProbeSent = false; initialWorld = null;
        began = phaseStart = EditorApplication.timeSinceStartup;
        Directory.CreateDirectory(Folder); Save("Starting"); EditorApplication.EnterPlaymode();
    }
    private static void OnLog(string message, string stack, LogType type)
    {
        if (!running || !EditorApplication.isPlaying || errors.Count >= 20) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack);
    }
    private static void Tick()
    {
        if (!running || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (Time.frameCount != lastFrame && Time.unscaledDeltaTime > 0 && Time.unscaledTime > 12)
            { frames.Add(Time.unscaledDeltaTime * 1000); lastFrame = Time.frameCount; }
            if (EditorApplication.timeSinceStartup < nextTick) return;
            nextTick = EditorApplication.timeSinceStartup + .5;
            var world = World.DefaultGameObjectInjectionWorld; if (world == null || !world.IsCreated) return;
            var em = world.EntityManager;
            using var bootstrap = em.CreateEntityQuery(typeof(PlayerBootstrapState));
            bool ready = bootstrap.CalculateEntityCount() == 1 && bootstrap.GetSingleton<PlayerBootstrapState>().Phase == PlayerBootstrapPhase.Ready;
            if (!ready) { Timeout(120, "Bootstrap"); return; }
            if (phase != 10 && initialWorld != null && world != initialWorld) throw new Exception("Unexpected world replacement");
            elapsed = Time.timeSinceLevelLoad;
            var workers = Owned<WorkerTag>(em, 1); var army = Owned<UnitComponent>(em, 1).Where(e => !em.HasComponent<WorkerTag>(e)).ToList();
            if (tower != Entity.Null && em.Exists(tower))
            {
                using var bullets = em.CreateEntityQuery(typeof(ArtilleryBullet)); using var entities = bullets.ToEntityArray(Allocator.Temp);
                foreach (var bullet in entities) if (em.GetComponentData<ArtilleryBullet>(bullet).sourceEntity == tower) shots.Add(bullet);
            }
            switch (phase)
            {
                case 0:
                    Time.timeScale = 2; // Verification only; normal player and restart use 1x.
                    initialWorld = world; home = Owned<MainBaseTag>(em, 1).Single(); enemyHome = Owned<MainBaseTag>(em, 2).Single();
                    if (workers.Count != 5) throw new Exception("Expected five local workers");
                    var types = new HashSet<ResourceType>();
                    foreach (var node in All<ResourceNodeData>(em)) if (!em.HasComponent<ResourceNodePendingConfig>(node)) types.Add(em.GetComponentData<ResourceNodeData>(node).Type);
                    if (types.Count != 3) throw new Exception("Resource types missing");
                    initialGold = Stock(em, ResourceType.Gold);
                    for (int i = 0; i < workers.Count; i++)
                    {
                        var type = i == 1 ? ResourceType.Wood : i == 2 ? ResourceType.Food : ResourceType.Gold;
                        var node = NearestResource(em, home, type);
                        CommandDataHelper.AddCommandToQueue(em, 1, workers[i], CommandType.TargetTo, targetEntity: node);
                        if (i == 0) { gold = node; initialNode = em.GetComponentData<ResourceNodeData>(gold).Amount; }
                    }
                    Pass("Main bootstrap: two bases, five workers, Gold/Wood/Food"); Next(1); break;
                case 1:
                    if (Stock(em, ResourceType.Gold) > initialGold && em.GetComponentData<ResourceNodeData>(gold).Amount < initialNode)
                    { Pass("Gather and paid drop-off increased player stock / depleted actual node"); OrderBuild(em, workers[3], "building.barracks"); Next(2); }
                    else Timeout(65, "Gather/drop-off"); break;
                case 2:
                    barracks = Building(em, "building.barracks");
                    if (Completed(em, barracks)) { Pass("Barracks built by worker"); OrderBuild(em, workers[4], "building.resourcedepot"); Next(3); }
                    else Timeout(55, "Barracks construction"); break;
                case 3:
                    depot = Building(em, "building.resourcedepot");
                    if (Completed(em, depot)) { Pass("Depot built by worker"); OrderBuild(em, workers[3], "building.defensetower"); Next(4); }
                    else Timeout(55, "Depot construction"); break;
                case 4:
                    tower = Building(em, "building.defensetower");
                    if (Completed(em, tower))
                    {
                        var bounds = em.GetComponentData<ConstructionVisualBounds>(tower);
                        var matrix = math.mul(em.GetComponentData<LocalTransform>(tower).ToMatrix(), em.GetComponentData<PostTransformMatrix>(tower).Value);
                        float expected = ConstructionRevealMath.Height(bounds, matrix, 1);
                        if (em.GetComponentData<RevealHeightProperty>(tower).Value + .01f < expected) throw new Exception("Tower reveal clips completed bounds");
                        var probe = Owned<UnitComponent>(em,2).FirstOrDefault(e => em.HasComponent<ShootAttack>(e));
                        if (probe != Entity.Null)
                        { CommandDataHelper.AddCommandToQueue(em,2,probe,CommandType.TargetTo,targetEntity:tower); raidProbeSent = true; }
                        Pass("Tower completed; reveal includes transformed mesh bounds"); Train(em, 3); Next(5);
                    }
                    else Timeout(55, "Tower construction"); break;
                case 5:
                    if (army.Count >= 3)
                    {
                        Pass("Three paid soldiers produced through Barracks FIFO");
                        moveStart = em.GetComponentData<LocalTransform>(army[0]).Position;
                        float3 target = em.GetComponentData<LocalTransform>(home).Position + new float3(-18, 0, 4);
                        foreach (var soldier in army) CommandDataHelper.AddCommandToQueue(em, 1, soldier, CommandType.Move, position: target);
                        Next(6);
                    }
                    else Timeout(55, "Production of three soldiers"); break;
                case 6:
                    movedFrom = army.Count > 0 ? math.distance(moveStart.xz, em.GetComponentData<LocalTransform>(army[0]).Position.xz) : 0;
                    if (movedFrom >= 5)
                    {
                        Pass("Soldier move command travelled >=5 world units");
                        foreach (var worker in workers) if (!em.IsComponentEnabled<BuilderComponent>(worker))
                            CommandDataHelper.AddCommandToQueue(em, 1, worker, CommandType.TargetTo, targetEntity: NearestResource(em, home, ResourceType.Gold));
                        Next(7);
                    }
                    else Timeout(40, "Soldier movement"); break;
                case 7:
                    int queued = em.GetBuffer<ProductionQueueElement>(barracks).Length;
                    if (army.Count + queued < 8 && Stock(em, ResourceType.Gold) >= 25) Train(em, 1);
                    if (army.Count >= 8) { Pass("Eight-soldier army funded by economy"); Next(8); }
                    else Timeout(100, "Army economy / production"); break;
                case 8:
                    if (PlayableSessionController.Instance != null && PlayableSessionController.Instance.Ended)
                    {
                        if (!PlayableSessionController.Instance.Won) throw new Exception("Autoplay lost the combat scenario");
                        Pass("Genuine combat destroyed enemy TownHall; victory UI active");
                        Pass("Tower projectiles observed: " + shots.Count);
                        PlayableLoopTools.CaptureGame(Folder + "/victory.png"); Next(9); break;
                    }
                    if (!em.Exists(home)) throw new Exception("Local base destroyed before victory");
                    if (em.Exists(barracks) && army.Count + em.GetBuffer<ProductionQueueElement>(barracks).Length < 8 && Stock(em, ResourceType.Gold) >= 25) Train(em, 1);
                    var enemyArmy = Owned<UnitComponent>(em, 2).Where(e => em.HasComponent<ShootAttack>(e)).ToList();
                    // A paid AI unit approaches the defense to exercise its actual weapons.
                    if (!raidProbeSent && enemyArmy.Count > 0)
                    { CommandDataHelper.AddCommandToQueue(em, 2, enemyArmy[0], CommandType.TargetTo, targetEntity: tower); raidProbeSent = true; }
                    Entity threat = Entity.Null; float distance = 45 * 45;
                    var basePosition = em.GetComponentData<LocalTransform>(home).Position;
                    foreach (var candidate in enemyArmy)
                    {
                        float d = math.distancesq(basePosition, em.GetComponentData<LocalTransform>(candidate).Position);
                        if (d < distance) { threat = candidate; distance = d; }
                    }
                    foreach (var soldier in army)
                    {
                        var soldierPosition = em.GetComponentData<LocalTransform>(soldier).Position;
                        Entity immediate = Entity.Null; float immediateDistance = 24 * 24;
                        foreach (var candidate in enemyArmy)
                        {
                            float d = math.distancesq(soldierPosition, em.GetComponentData<LocalTransform>(candidate).Position);
                            if (d < immediateDistance) { immediate = candidate; immediateDistance = d; }
                        }
                        if (elapsed >= 100 && em.Exists(enemyHome)) CommandDataHelper.AddCommandToQueue(em, 1, soldier, CommandType.TargetTo, targetEntity: enemyHome);
                        else if (immediate != Entity.Null) CommandDataHelper.AddCommandToQueue(em, 1, soldier, CommandType.TargetTo, targetEntity: immediate);
                        else if (threat != Entity.Null && elapsed < 100) CommandDataHelper.AddCommandToQueue(em, 1, soldier, CommandType.TargetTo, targetEntity: threat);
                        else if (elapsed >= 100)
                        {
                            var position = em.GetComponentData<LocalTransform>(soldier).Position;
                            Entity nearby = Entity.Null; float closest = 24 * 24;
                            foreach (var candidate in enemyArmy)
                            {
                                float d = math.distancesq(position, em.GetComponentData<LocalTransform>(candidate).Position);
                                if (d < closest) { nearby = candidate; closest = d; }
                            }
                            if (em.Exists(enemyHome)) CommandDataHelper.AddCommandToQueue(em, 1, soldier, CommandType.TargetTo, targetEntity: nearby != Entity.Null ? nearby : enemyHome);
                        }
                        else CommandDataHelper.AddCommandToQueue(em, 1, soldier, CommandType.Move,
                            position: em.GetComponentData<LocalTransform>(tower).Position + new float3(7, 0, 7));
                    }
                    Timeout(330, "Combat / victory"); break;
                case 9:
                    if (EditorApplication.timeSinceStartup - phaseStart > 1) { PlayableSessionController.Instance.RestartMatch(); Next(10); }
                    break;
                case 10:
                    if (world != initialWorld && workers.Count == 5 && Owned<MainBaseTag>(em, 1).Count == 1 && Owned<MainBaseTag>(em, 2).Count == 1 && army.Count == 0)
                    {
                        Pass("Restart: new ECS world, exactly two bases, five workers, no leftover army");
                        initialWorld = world;
                        PlayableLoopTools.CaptureGame(Folder + "/restart.png"); Next(11);
                    }
                    else Timeout(100, "Restart cleanup / bootstrap"); break;
                case 11:
                    if (EditorApplication.timeSinceStartup - phaseStart > 1) Finish(true, "Full playable loop completed"); break;
            }
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }
    private static void Train(EntityManager em, int count)
    {
        var offer = em.GetBuffer<ProductionElement>(barracks).ToNativeArray(Allocator.Temp);
        try { foreach (var entry in offer) if (entry.Kind == ProductionKind.Train) { for (int i = 0; i < count; i++) CommandDataHelper.AddCommandToQueue(em, 1, barracks, CommandType.Progression, progression: entry.JobReference); break; } }
        finally { offer.Dispose(); }
    }
    private static void OrderBuild(EntityManager em, Entity worker, string id)
    {
        var prefab = SkirmishOpponent.FindBuildingPrefab(em, id);
        if (!SkirmishOpponent.TryBuildPosition(em, prefab, home, out var position)) throw new Exception("No valid construction site for " + id);
        CommandDataHelper.AddPlanBuildToQueue(em, 1, new[] { worker }, prefab, position);
    }
    private static Entity Building(EntityManager em, string id) => Owned<BuildingComponent>(em, 1).FirstOrDefault(e => em.GetComponentData<BuildingComponent>(e).DefinitionID == id);
    private static bool Completed(EntityManager em, Entity entity) => em.Exists(entity) && em.GetComponentData<BuildingConstruction>(entity).Phase == ConstructionPhase.Completed;
    private static List<Entity> All<T>(EntityManager em) where T : unmanaged, IComponentData
    { using var query = em.CreateEntityQuery(typeof(T)); using var array = query.ToEntityArray(Allocator.Temp); return array.ToArray().ToList(); }
    private static List<Entity> Owned<T>(EntityManager em, int owner) where T : unmanaged, IComponentData => All<T>(em).Where(e =>
        em.HasComponent<EntityOwner>(e) && em.GetComponentData<EntityOwner>(e).PlayerID == owner && (!em.HasComponent<EntityHealth>(e) || em.GetComponentData<EntityHealth>(e).CurrentHP > 0)).ToList();
    private static Entity NearestResource(EntityManager em, Entity near, ResourceType type)
    {
        var position = em.GetComponentData<LocalTransform>(near).Position;
        return All<ResourceNodeData>(em).Where(e => !em.HasComponent<ResourceNodePendingConfig>(e) && em.GetComponentData<ResourceNodeData>(e).Type == type && em.GetComponentData<ResourceNodeData>(e).Amount > 0)
            .OrderBy(e => math.distancesq(position, em.GetComponentData<LocalTransform>(e).Position)).First();
    }
    private static float Stock(EntityManager em, ResourceType type)
    {
        PlayerContextHelper.GetPlayerContextEntity(em, 1, out var player, out _);
        foreach (var resource in em.GetBuffer<ResourcePair>(player)) if (resource.Type == type) return resource.Amount;
        return 0;
    }
    private static void Next(int next) { phase = next; phaseStart = EditorApplication.timeSinceStartup; Save("Running phase " + phase); }
    private static void Pass(string message) { checks.Add(message); Save("Running phase " + phase); }
    private static void Timeout(double seconds, string label) { if (EditorApplication.timeSinceStartup - phaseStart > seconds) throw new Exception(label + " timed out"); }
    private static void Finish(bool success, string message)
    {
        Time.timeScale = 1;
        running = false; SessionState.SetBool(Key, false);
        if (!success) errors.Add(message); Save(success && errors.Count == 0 ? "PASS" : "FAIL");
        if (success) EditorApplication.ExitPlaymode();
    }
    private static void Save(string status)
    {
        Directory.CreateDirectory(Folder); var sorted = frames.OrderBy(x => x).ToArray();
        float median = sorted.Length == 0 ? 0 : sorted[sorted.Length / 2], p95 = sorted.Length == 0 ? 0 : sorted[Math.Min(sorted.Length - 1, (int)(sorted.Length * .95))];
        File.WriteAllText(Folder + "/smoke-report.txt", status + "\nEditor simulation: 2x until restart (normal game 1x)\nPhase: " + phase + "\nElapsed wall: " + (EditorApplication.timeSinceStartup - began).ToString("0.0") +
            "s\nEditor frame median/p95: " + median.ToString("0.0") + "/" + p95.ToString("0.0") + "ms\n" + string.Join("\n", checks.Select(c => "PASS: " + c)) +
            "\n" + string.Join("\n", errors.Select(e => "ERROR: " + e)));
    }
}
