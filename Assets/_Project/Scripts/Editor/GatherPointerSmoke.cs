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

/// <summary>Opt-in regression of the screen-pointer gather path, not direct ECS commands.</summary>
[InitializeOnLoad]
public static class GatherPointerSmoke
{
    private const string Key = "RTS.GatherPointerSmoke";
    private static bool running;
    private static double started;
    private static bool ordered;
    private static readonly List<string> checks = new();
    private static readonly List<string> errors = new();
    private static readonly Dictionary<ResourceType, float> stocks = new();
    private static readonly Dictionary<ResourceType, Entity> nodes = new();
    private static readonly Dictionary<ResourceType, int> amounts = new();
    static GatherPointerSmoke()
    {
        running = SessionState.GetBool(Key, false); started = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, stack, type) =>
        { if (running && (type == LogType.Error || type == LogType.Exception)) errors.Add(message + "\n" + stack); };
    }
    [MenuItem("Tools/RTS UI/Verify pointer gathering")]
    public static void Begin()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before pointer smoke.");
        SessionState.SetBool(Key, true); running = true; ordered = false;
        checks.Clear(); errors.Clear(); stocks.Clear(); nodes.Clear(); amounts.Clear();
        started = EditorApplication.timeSinceStartup; Save("Starting"); EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        if (!running || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (EditorApplication.timeSinceStartup - started > 90) throw new Exception("Pointer gather / delivery timed out");
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;
            var em = world.EntityManager;
            using var bootstrap = em.CreateEntityQuery(typeof(PlayerBootstrapState));
            if (bootstrap.CalculateEntityCount() != 1 || bootstrap.GetSingleton<PlayerBootstrapState>().Phase != PlayerBootstrapPhase.Ready) return;
            if (Camera.main == null || UnitController.Instance == null) return;
            if (Time.timeSinceLevelLoad < 3) return; // Allow snap/height and physics-world publication to settle.
            if (!SelectHelper.TryGetLocalPlayerId(out int owner)) return;
            PlayerContextHelper.GetPlayerContextEntity(em, owner, out var player, out _);
            if (!ordered)
            {
                using var workerQuery = em.CreateEntityQuery(typeof(WorkerTag), typeof(EntityOwner));
                using var workers = workerQuery.ToEntityArray(Allocator.Temp);
                var local = workers.ToArray().Where(e => em.GetComponentData<EntityOwner>(e).PlayerID == owner).ToArray();
                if (local.Length < 3) throw new Exception("Three local workers required");
                using var resourceQuery = em.CreateEntityQuery(typeof(ResourceNodeData), typeof(LocalTransform));
                using var resources = resourceQuery.ToEntityArray(Allocator.Temp);
                int i = 0;
                foreach (ResourceType type in new[] { ResourceType.Gold, ResourceType.Wood, ResourceType.Food })
                {
                    var worker = local[i++];
                    var position = em.GetComponentData<LocalTransform>(worker).Position;
                    var camera = Camera.main; var saved = camera.transform.position;
                    try
                    {
                        Entity node = Entity.Null; Vector2 point = default;
                        foreach (var candidate in resources.ToArray().Where(e => !em.HasComponent<ResourceNodePendingConfig>(e) && em.GetComponentData<ResourceNodeData>(e).Type == type)
                            .OrderBy(e => math.distancesq(position, em.GetComponentData<LocalTransform>(e).Position)))
                        {
                            var transform = em.GetComponentData<LocalTransform>(candidate);
                            var center = transform.Position;
                            if (em.HasComponent<Unity.Physics.PhysicsCollider>(candidate))
                            {
                                var collider = em.GetComponentData<Unity.Physics.PhysicsCollider>(candidate).Value;
                                center = collider.Value.CalculateAabb(new RigidTransform(transform.Rotation, transform.Position), transform.Scale).Center;
                            }
                            camera.transform.position = (Vector3)center - camera.transform.forward * 60;
                            foreach (float height in new[] { 0f, .2f, .6f, 1f, 2f })
                            {
                                point = camera.WorldToScreenPoint((Vector3)center + Vector3.up * height);
                                if (ResourceInspection.TryResolveNode(camera, point, em, out var hit) && hit == candidate) { node = candidate; break; }
                            }
                            if (node != Entity.Null) break; // Do not click through a TownHall hiding a small bush.
                        }
                        if (node == Entity.Null) throw new Exception(type + " has no visible pickable resource node");
                        bool legacy = UnityEngine.Physics.Raycast(camera.ScreenPointToRay(point), out var visual, 500) && visual.collider.GetComponentInParent<ResourceNodeReference>() != null;
                        checks.Add(type + ": pointer picks baked ECS node; legacy GameObject resource hit=" + legacy);
                        using var selectionQuery = em.CreateEntityQuery(ComponentType.ReadOnly<Selected>());
                        using var selection = selectionQuery.ToEntityArray(Allocator.Temp);
                        foreach (var entity in selection) em.SetComponentEnabled<Selected>(entity, false);
                        if (UnitController.Instance.TryCommandGather(em, camera, point)) throw new Exception("Command accepted without worker selection");
                        em.SetComponentEnabled<Selected>(worker, true);
                        if (!ResourceInspection.TryInspect(camera, point, em, owner) || !em.IsComponentEnabled<Selected>(worker))
                            throw new Exception("Inspection did not retain selected worker");
                        if (!UnitController.Instance.TryCommandGather(em, camera, point)) throw new Exception(type + " pointer command rejected");
                        nodes[type] = node; amounts[type] = em.GetComponentData<ResourceNodeData>(node).Amount;
                        stocks[type] = Stock(em, player, type);
                        checks.Add(type + ": runtime pointer command accepted for selected worker; empty selection rejected");
                    }
                    finally { camera.transform.position = saved; }
                }
                ResourceInspection.Clear(); ordered = true; Save("Running: waiting for real gather and delivery");
            }
            if (ordered && nodes.All(pair => em.Exists(pair.Value) && em.GetComponentData<ResourceNodeData>(pair.Value).Amount < amounts[pair.Key] && Stock(em, player, pair.Key) > stocks[pair.Key]))
            {
                checks.Add("Gold, Wood and Food: actual node depleted and paid stock increased after worker delivery");
                Finish(true, "Completed");
            }
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }
    private static float Stock(EntityManager em, Entity player, ResourceType type)
    { foreach (var pair in em.GetBuffer<ResourcePair>(player)) if (pair.Type == type) return pair.Amount; return 0; }
    private static void Finish(bool success, string message)
    {
        if (!success) errors.Add(message);
        running = false; SessionState.SetBool(Key, false); Save(success && errors.Count == 0 ? "PASS" : "FAIL");
        if (success) EditorApplication.ExitPlaymode();
    }
    private static void Save(string status)
    {
        Directory.CreateDirectory("Temp/PlayableLoop");
        File.WriteAllText("Temp/PlayableLoop/gather-pointer-report.txt", status + "\n" + string.Join("\n", checks) + "\n" + string.Join("\n", errors));
        Directory.CreateDirectory("Artifacts/Verification/GatherPointer");
        File.Copy("Temp/PlayableLoop/gather-pointer-report.txt", "Artifacts/Verification/GatherPointer/report.txt", true);
    }
}
