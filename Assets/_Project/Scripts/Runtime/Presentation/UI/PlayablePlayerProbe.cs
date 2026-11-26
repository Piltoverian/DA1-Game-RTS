using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using Unity.Entities;
using UnityEngine;

/// <summary>Explicit command-line startup/render probe for the built player; inactive during normal play.</summary>
public sealed class PlayablePlayerProbe : MonoBehaviour
{
    [Serializable] private class Report
    {
        public bool ready, hud;
        public bool ended, won;
        public string version, graphics, error;
        public int gridWidth, workers, bases, resourceTypes, frames;
        public float medianMs, p95Ms;
    }
    private readonly List<float> times = new();
    private readonly List<string> errors = new();
    private string folder;
    private float readyAt = -1;
    private bool finished;
    private bool verifyDefeat;
    private void Awake()
    {
        var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-rts-verify");
        verifyDefeat = Array.IndexOf(args,"-rts-verify-defeat") >= 0;
        folder = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.Combine(Application.persistentDataPath, "Verification");
        Directory.CreateDirectory(folder); Application.logMessageReceived += OnLog;
    }
    private void OnLog(string message, string stack, LogType type)
    { if (errors.Count < 12 && (type == LogType.Error || type == LogType.Exception)) errors.Add(message + "\n" + stack); }
    private void Update()
    {
        if (finished) return;
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) { if (Time.realtimeSinceStartup > 90) Finish(false); return; }
        using var bootstrap = world.EntityManager.CreateEntityQuery(typeof(PlayerBootstrapState));
        bool ready = bootstrap.CalculateEntityCount() == 1 && bootstrap.GetSingleton<PlayerBootstrapState>().Phase == PlayerBootstrapPhase.Ready;
        if (!ready) { if (Time.realtimeSinceStartup > 90) Finish(false); return; }
        if (readyAt < 0) readyAt = Time.realtimeSinceStartup;
        float elapsed = Time.realtimeSinceStartup - readyAt;
        if (elapsed > 4) times.Add(Time.unscaledDeltaTime * 1000);
        if (verifyDefeat)
        {
            if (PlayableSessionController.Instance != null && PlayableSessionController.Instance.Ended) Finish(!PlayableSessionController.Instance.Won);
            else if (elapsed > 260) { errors.Add("Idle-player defeat check timed out"); Finish(false); }
        }
        else if (elapsed > 18) Finish(true);
    }
    private void Finish(bool ready)
    {
        finished = true; times.Sort();
        var report = new Report { ready = ready, version = Application.unityVersion, graphics = SystemInfo.graphicsDeviceName,
            frames = times.Count, hud = FindFirstObjectByType<PlayableSessionController>() != null && FindFirstObjectByType<TMP_Text>() != null,
            error = string.Join("\n", errors) };
        report.ended = PlayableSessionController.Instance != null && PlayableSessionController.Instance.Ended;
        report.won = report.ended && PlayableSessionController.Instance.Won;
        if (times.Count > 0) { report.medianMs = times[times.Count / 2]; report.p95Ms = times[Mathf.Min(times.Count - 1, (int)(times.Count * .95f))]; }
        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null && world.IsCreated)
        {
            var em = world.EntityManager;
            using var grids = em.CreateEntityQuery(typeof(GridComponent)); if (grids.CalculateEntityCount() == 1) report.gridWidth = grids.GetSingleton<GridComponent>().width;
            using var workers = em.CreateEntityQuery(typeof(WorkerTag)); report.workers = workers.CalculateEntityCount();
            using var bases = em.CreateEntityQuery(typeof(MainBaseTag)); report.bases = bases.CalculateEntityCount();
            using var resources = em.CreateEntityQuery(typeof(ResourceNodeData));
            using var nodes = resources.ToComponentDataArray<ResourceNodeData>(Unity.Collections.Allocator.Temp);
            var types = new HashSet<ResourceType>(); foreach (var node in nodes) if (node.Amount > 0) types.Add(node.Type); report.resourceTypes = types.Count;
        }
        File.WriteAllText(Path.Combine(folder,"player-report.json"), JsonUtility.ToJson(report,true));
        ScreenCapture.CaptureScreenshot(Path.Combine(folder,"player.png"));
        if (Array.IndexOf(Environment.GetCommandLineArgs(),"-rts-verify-quit") >= 0) Invoke(nameof(Quit),2);
    }
    private void Quit() => Application.Quit();
    private void OnDestroy() => Application.logMessageReceived -= OnLog;
}
