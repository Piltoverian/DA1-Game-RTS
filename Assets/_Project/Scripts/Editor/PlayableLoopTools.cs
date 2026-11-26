using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Explicit local requests only. Keeps Play Mode/build verification reproducible in an open Editor.
[InitializeOnLoad]
public static class PlayableLoopTools
{
    private const string Folder = "Temp/PlayableLoop";
    private static double nextPoll;
    static PlayableLoopTools() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling) return;
        nextPoll = EditorApplication.timeSinceStartup + .5;
        string request = Folder + "/request.txt";
        if (!File.Exists(request)) return;
        string command = File.ReadAllText(request).Trim(); File.Delete(request);
        try
        {
            switch (command)
            {
                case "snapshot": Snapshot(); break;
                case "play": EditorApplication.EnterPlaymode(); break;
                case "stop": SessionState.SetBool("RTS.PlayableLoopSmoke", false); EditorApplication.ExitPlaymode(); break;
                case "refresh": AssetDatabase.Refresh(); break;
                case "content": BuildingContentVerification.Verify(); break;
                case "setup": PlayableDemoSetup.Prepare(); break;
                case "smoke": PlayableLoopSmoke.Begin(); break;
                case "gather-smoke": GatherPointerSmoke.Begin(); break;
                case "capture": CaptureGame(Folder + "/game-view.png"); break;
                case "build": BuildWindows(); break;
            }
        }
        catch (Exception error) { Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/tool-error.txt", error.ToString()); Debug.LogException(error); }
    }
    [MenuItem("Tools/RTS UI/Build playable Windows demo")]
    public static void BuildWindows()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before building.");
        Directory.CreateDirectory("Builds/Windows");
        PlayerSettings.productName = "Immortal RTS Demo";
        PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/_Project/Scenes/Main.unity" },
            locationPathName = "Builds/Windows/ImmortalRTS.exe", target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        File.WriteAllText(Folder + "/build-result.txt", report.summary.result + "\nErrors: " + report.summary.totalErrors +
            "\nWarnings: " + report.summary.totalWarnings + "\nSize: " + report.summary.totalSize);
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Windows build failed; inspect Console.");
    }
    public static void CaptureGame(string path)
    {
        var view = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
        view.Focus(); view.Repaint(); EditorApplication.QueuePlayerLoopUpdate();
        EditorApplication.delayCall += () => ScreenCapture.CaptureScreenshot(path);
    }
    [Serializable] private class Report
    {
        public bool playing;
        public string world, bootstrap, error;
        public int gridWidth, gridHeight;
        public float cellSize;
        public float gameTime;
        public List<Item> entities = new();
    }
    [Serializable] private class Item
    {
        public int index, owner = -1, amount, queue;
        public string definition, resource;
        public Vector3 position;
        public float hp, work;
        public bool moving, moveOverride, shooting;
        public int target;
        public string stopReason;
        public Vector3 destination, realTarget, velocity, forward;
        public float weaponTimer;
    }
    private static void Snapshot()
    {
        var report = new Report { playing = EditorApplication.isPlaying, gameTime = Time.timeSinceLevelLoad };
        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null && world.IsCreated)
        {
            report.world = world.Name; var em = world.EntityManager;
            using var bootstrap = em.CreateEntityQuery(typeof(PlayerBootstrapState));
            if (bootstrap.CalculateEntityCount() == 1) report.bootstrap = bootstrap.GetSingleton<PlayerBootstrapState>().Phase.ToString();
            using var grids = em.CreateEntityQuery(typeof(GridComponent));
            if (grids.CalculateEntityCount() == 1) { var grid = grids.GetSingleton<GridComponent>(); report.gridWidth = grid.width; report.gridHeight = grid.height; report.cellSize = grid.cellsize; }
            using var query = em.CreateEntityQuery(typeof(LocalTransform));
            using var entities = query.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                if (!em.HasComponent<UnitComponent>(entity) && !em.HasComponent<BuildingComponent>(entity) && !em.HasComponent<ResourceNodeData>(entity)) continue;
                var item = new Item { index = entity.Index, position = em.GetComponentData<LocalTransform>(entity).Position };
                if (em.HasComponent<EntityOwner>(entity)) item.owner = em.GetComponentData<EntityOwner>(entity).PlayerID;
                if (em.HasComponent<EntityHealth>(entity)) item.hp = em.GetComponentData<EntityHealth>(entity).CurrentHP;
                if (em.HasComponent<UnitComponent>(entity)) item.definition = em.GetComponentData<UnitComponent>(entity).DefinitionID.ToString();
                if (em.HasComponent<BuildingComponent>(entity)) item.definition = em.GetComponentData<BuildingComponent>(entity).DefinitionID.ToString();
                if (em.HasComponent<BuildingConstruction>(entity)) item.work = em.GetComponentData<BuildingConstruction>(entity).CompletedWork;
                if (em.HasBuffer<ProductionQueueElement>(entity)) item.queue = em.GetBuffer<ProductionQueueElement>(entity).Length;
                if (em.HasComponent<ResourceNodeData>(entity)) { var data = em.GetComponentData<ResourceNodeData>(entity); item.resource = data.Type.ToString(); item.amount = data.Amount; }
                if (em.HasComponent<MovementAgentComponent>(entity))
                { var data=em.GetComponentData<MovementAgentComponent>(entity); item.moving=data.hastarget;item.destination=data.currentworldtarget;item.realTarget=data.realTarget;item.velocity=data.velocity;item.stopReason=data.stopReason.ToString(); }
                if (em.HasComponent<MoveOverride>(entity)) item.moveOverride=em.IsComponentEnabled<MoveOverride>(entity);
                if (em.HasComponent<ShootAttack>(entity)) item.shooting=em.IsComponentEnabled<ShootAttack>(entity);
                if (em.HasComponent<Target>(entity)) item.target=em.GetComponentData<Target>(entity).targetEntity.Index;
                item.forward=mathForward(em.GetComponentData<LocalTransform>(entity).Rotation);
                if (em.HasBuffer<UnitWeaponSlot>(entity) && em.GetBuffer<UnitWeaponSlot>(entity).Length>0) item.weaponTimer=em.GetBuffer<UnitWeaponSlot>(entity)[0].timer;
                report.entities.Add(item);
            }
        }
        Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/snapshot.json", JsonUtility.ToJson(report, true));
    }
    private static Vector3 mathForward(Unity.Mathematics.quaternion rotation) => Unity.Mathematics.math.mul(rotation,Unity.Mathematics.math.forward());
}
