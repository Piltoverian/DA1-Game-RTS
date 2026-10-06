using System;
using System.IO;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

[InitializeOnLoad]
public static class SelectionSmokeTest
{
    const string Request = "Artifacts/SelectionSmoke/request.txt";
    const string Report = "Artifacts/SelectionSmoke/report.txt";
    const string Stage = "SelectionSmoke.Stage";
    static Entity worker;
    static int player;
    static double deadline, next;
    static bool startedPlay;
    static string details;
    static readonly List<Entity> previousSelection = new List<Entity>();

    static SelectionSmokeTest() { EditorApplication.update += Tick; }

    [MenuItem("Tools/Selection/Test spawned worker selection")]
    public static void Start()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!EditorApplication.isPlaying)
        {
            if (EditorSceneManager.GetActiveScene().isDirty)
            { Write("BLOCKED: Current scene has unsaved changes. Save it before running this test."); return; }
            startedPlay = true;
            EditorApplication.isPlaying = true;
        }
        else startedPlay = false;
        SessionState.SetBool("SelectionSmoke.StartedPlay", startedPlay);
        deadline = EditorApplication.timeSinceStartup + 90;
        details = "";
        SessionState.SetInt(Stage, 1);
    }

    static void Tick()
    {
        if (File.Exists(Request) && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
        { File.Delete(Request); Start(); }
        int stage = SessionState.GetInt(Stage, 0);
        if (stage == 0 || !EditorApplication.isPlaying) return;
        if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 90;
        try
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;
            var em = world.EntityManager;
            if (stage == 1)
            {
                using var local = em.CreateEntityQuery(typeof(LocalTestPlayer));
                using var bootstrap = em.CreateEntityQuery(typeof(PlayerBootstrapState));
                using var manager = em.CreateEntityQuery(typeof(DOTSSelectManagerComponent), typeof(SelectionRequest));
                using var physics = em.CreateEntityQuery(typeof(PhysicsWorldSingleton));
                using var workers = em.CreateEntityQuery(typeof(UnitComponent), typeof(EntityOwner), typeof(Selectable), typeof(LocalTransform));
                details = $"local={local.CalculateEntityCount()}, bootstrap={bootstrap.CalculateEntityCount()}, manager={manager.CalculateEntityCount()}, physics={physics.CalculateEntityCount()}, units={workers.CalculateEntityCount()}\n";
                if (bootstrap.CalculateEntityCount() == 1) details += "bootstrap phase=" + bootstrap.GetSingleton<PlayerBootstrapState>().Phase + "\n";
                if (local.CalculateEntityCount() != 1 || manager.CalculateEntityCount() != 1 || physics.CalculateEntityCount() != 1 ||
                    bootstrap.CalculateEntityCount() != 1 || bootstrap.GetSingleton<PlayerBootstrapState>().Phase != PlayerBootstrapPhase.Ready)
                { if (EditorApplication.timeSinceStartup > deadline) Finish("FAIL: Readiness gate blocked.\n" + details); return; }
                player = local.GetSingleton<LocalTestPlayer>().PlayerId;
                using var units = workers.ToEntityArray(Allocator.Temp);
                worker = Entity.Null;
                foreach (var unit in units) if (em.GetComponentData<EntityOwner>(unit).PlayerID == player) { worker = unit; break; }
                if (worker == Entity.Null) { Finish("FAIL: No worker owned by local player " + player + "\n" + details); return; }
                var pos = em.GetComponentData<LocalTransform>(worker).Position;
                details += $"player={player}, worker={worker}, selectableOwner={em.GetComponentData<Selectable>(worker).playerID}, selectedTag={em.HasComponent<Selected>(worker)}, dragTag={em.HasComponent<DragSelectableEntity>(worker)}, pos={pos}\n";
                var selectManager = GameManager.Instance.GetModule<SelectManager>();
                bool inputPlayerReady = selectManager != null && selectManager.TryGetPlayerId(out _);
                details += $"inputPlayerReady={inputPlayerReady}, camera={Camera.main}\n";
                if (Camera.main != null)
                {
                    var screen = Camera.main.WorldToScreenPoint(pos + new float3(0, 1, 0));
                    details += $"workerScreen={screen}, screenSize={Screen.width}x{Screen.height}\n";
                    var screenRay = SelectManager.PhysicConvertHelper.GetRayCastInput(screen, Camera.main, uint.MaxValue);
                    bool screenHit = physics.GetSingleton<PhysicsWorldSingleton>().CastRay(screenRay, out var cameraHit);
                    details += $"cameraRayHit={screenHit}, entity={cameraHit.Entity}, selectable={em.HasComponent<Selectable>(cameraHit.Entity)}\n";
                    if (EventSystem.current != null)
                    {
                        var uiHits = new List<RaycastResult>();
                        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, uiHits);
                        foreach (var uiHit in uiHits) details += $"pointerBlocker={uiHit.gameObject.name}, module={uiHit.module}\n";
                    }
                }
                var ray = new RaycastInput { Start = pos + new float3(0, 20, 0), End = pos - new float3(0, 20, 0),
                    Filter = new CollisionFilter { BelongsTo = uint.MaxValue, CollidesWith = uint.MaxValue } };
                bool hit = physics.GetSingleton<PhysicsWorldSingleton>().CastRay(ray, out Unity.Physics.RaycastHit result);
                details += $"rayHit={hit}, hitEntity={result.Entity}, hitSelectable={em.HasComponent<Selectable>(result.Entity)}\n";
                previousSelection.Clear();
                using (var selectedQuery = em.CreateEntityQuery(typeof(Selected)))
                using (var selectedEntities = selectedQuery.ToEntityArray(Allocator.Temp))
                    foreach (var selectedEntity in selectedEntities) previousSelection.Add(selectedEntity);
                em.GetBuffer<SelectionRequest>(manager.GetSingletonEntity()).Add(new SelectionRequest { mode = SelectionMode.Click, playerId = player, rayInput = ray });
                next = EditorApplication.timeSinceStartup + 0.5;
                SessionState.SetInt(Stage, 2);
            }
            else if (EditorApplication.timeSinceStartup >= next)
            {
                if (!em.Exists(worker)) { Finish("FAIL: Worker disappeared.\n" + details); return; }
                bool selected = em.HasComponent<Selected>(worker) && em.IsComponentEnabled<Selected>(worker);
                details += (stage == 2 ? "clickSelected=" : stage == 3 ? "dragSelected=" : "inputClickSelected=") + selected + "\n";
                if (stage == 2)
                {
                    if (em.HasComponent<Selected>(worker)) em.SetComponentEnabled<Selected>(worker, false);
                    var pos = em.GetComponentData<LocalTransform>(worker).Position;
                    using var query = em.CreateEntityQuery(typeof(SelectionRequest));
                    em.GetBuffer<SelectionRequest>(query.GetSingletonEntity()).Add(new SelectionRequest { mode = SelectionMode.Drag, playerId = player,
                        v1 = pos + new float3(-2, 0, -2), v2 = pos + new float3(2, 0, 2),
                        v3 = pos + new float3(-2, 0, 2), v4 = pos + new float3(2, 0, -2) });
                    next = EditorApplication.timeSinceStartup + 0.5;
                    SessionState.SetInt(Stage, 3);
                }
                else if (stage == 3)
                {
                    if (em.HasComponent<Selected>(worker)) em.SetComponentEnabled<Selected>(worker, false);
                    if (Camera.main == null) { Finish("FAIL: No camera for input selection\n" + details); return; }
                    var pos = em.GetComponentData<LocalTransform>(worker).Position;
                    var screen = Camera.main.WorldToScreenPoint(pos + new float3(0, 1, 0));
                    GameManager.Instance.GetModule<SelectManager>().SingleSelecting(screen, em);
                    next = EditorApplication.timeSinceStartup + 0.5;
                    SessionState.SetInt(Stage, 4);
                }
                else Finish(details.Contains("clickSelected=True") && details.Contains("dragSelected=True") &&
                    details.Contains("inputPlayerReady=True") && selected ? "PASS\n" + details : "FAIL\n" + details);
            }
        }
        catch (Exception error) { Finish("FAIL: " + error + "\n" + details); }
    }

    static void Finish(string result)
    {
        SessionState.SetInt(Stage, 0);
        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null && world.IsCreated)
        {
            var em = world.EntityManager;
            using var query = em.CreateEntityQuery(typeof(Selected));
            using var selected = query.ToEntityArray(Allocator.Temp);
            foreach (var entity in selected) em.SetComponentEnabled<Selected>(entity, false);
            foreach (var entity in previousSelection)
                if (em.Exists(entity) && em.HasComponent<Selected>(entity)) em.SetComponentEnabled<Selected>(entity, true);
        }
        Write(result);
        if (SessionState.GetBool("SelectionSmoke.StartedPlay", false)) EditorApplication.isPlaying = false;
    }
    static void Write(string value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Report));
        File.WriteAllText(Report, value);
        Debug.Log("Selection smoke: " + value);
    }
}
