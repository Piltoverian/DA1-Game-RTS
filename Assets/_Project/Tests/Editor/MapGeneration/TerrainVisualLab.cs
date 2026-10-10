using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Object = UnityEngine.Object;

// Editor-only lab: uses the production generator, visual compiler and renderer.
// Current bounded checks only. Commands: regression, main-current. Capture never saves Main.
[InitializeOnLoad]
public static class TerrainVisualLab
{
    public const string Folder = "Artifacts/TerrainVisual";
    public const string ThemePath = TerrainMaterialAuthoring.ThemePath;
    const string Pending = "TerrainVisualLab.Pending";
    const string Started = "TerrainVisualLab.Started";
    static int lastSampleFrame=-1,readyFrames;
    static readonly List<double> frameSamples=new List<double>();
    static TerrainVisualLab() { EditorApplication.update += Tick; }
    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string request = Folder + "/request.txt";
        if (File.Exists(request))
        {
            string command = File.ReadAllText(request).Trim(); File.Delete(request);
            try
            {
                if (command == "main-current")
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play before Main capture.");
                    var mapRenderer=Object.FindFirstObjectByType<TerrainMapRenderer>();
                    if(!mapRenderer||AssetDatabase.GetAssetPath(mapRenderer.theme)!=ThemePath)throw new Exception("Main must use the current validated theme.");
                    readyFrames=0;lastSampleFrame=-1;frameSamples.Clear();
                    SessionState.SetString(Pending,command);SessionState.SetString(Started,DateTime.UtcNow.ToString("O"));EditorApplication.isPlaying=true;
                }
                else if(command=="regression")
                {
                    CanonicalTerrainValidation.RunCleanupSuite();
                    CanonicalTerrainValidation.RunWithTheme(AssetDatabase.LoadAssetAtPath<TerrainTheme>(ThemePath));
                    File.WriteAllText(Folder+"/regression.txt","PASS current theme, reference geometry, offset/restore, picking, generation and resource planner.");
                }
                else throw new Exception("Unknown current lab request: "+command);
            }
            catch (Exception e) { File.WriteAllText(Folder + "/failure.txt", e.ToString()); UnityEngine.Debug.LogException(e); }
        }
        string pending = SessionState.GetString(Pending, "");
        if (pending.Length == 0 || !EditorApplication.isPlaying) return;
        try
        {
            if((DateTime.UtcNow-DateTime.Parse(SessionState.GetString(Started, ""),null,System.Globalization.DateTimeStyles.RoundtripKind)).TotalSeconds>600)throw new Exception("Main capture timed out before Ready.");
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;
            var em = world.EntityManager;
            using var roots = em.CreateEntityQuery(typeof(PlayerBootstrapState));
            if (roots.IsEmptyIgnoreFilter) return;
            var phase = em.GetComponentData<PlayerBootstrapState>(roots.GetSingletonEntity()).Phase;
            if (phase == PlayerBootstrapPhase.Failed) throw new Exception("Main bootstrap failed.");
            if (phase != PlayerBootstrapPhase.Ready) return;
            var renderer = Object.FindFirstObjectByType<TerrainChunkRenderer>();
            if (!renderer || !renderer.PresentationRoot) return;
            // Comparable fixed-camera editor Play frame samples after bootstrap, excluding its stall.
            if(readyFrames<240){
                if(Time.frameCount==lastSampleFrame)return;lastSampleFrame=Time.frameCount;
                if(readyFrames==0){
                    using var frameGrid=em.CreateEntityQuery(typeof(GridComponent),typeof(GridSpawnCell));var frameEntity=frameGrid.GetSingletonEntity();
                    var frameSpawns=em.GetBuffer<GridSpawnCell>(frameEntity);var frameConfig=em.GetComponentData<GridComponent>(frameEntity);
                    var target=(Vector3)GridHelper.GridToWorld(frameSpawns[0].cell,frameConfig);target.y=TerrainVisualSurface.Active.Sample(target.x,target.z);
                    Camera.main.transform.position=target-Camera.main.transform.forward*80;Camera.main.orthographicSize=18;
                    var setup=Camera.main.GetComponent<IsometricTerrainCamera>();if(setup)setup.size=18;
                }
                if(readyFrames>=60)frameSamples.Add(Time.unscaledDeltaTime*1000);
                readyFrames++;return;
            }
            if(frameSamples.Count>0){
                frameSamples.Sort();double total=0;foreach(double value in frameSamples)total+=value;
                Directory.CreateDirectory(Folder+"/"+pending);File.WriteAllText(Folder+"/"+pending+"/editor-frame-times.txt",$"Editor Play, same seed and spawn0 camera/zoom18; 60 warm-up + {frameSamples.Count} samples; medianMs={frameSamples[frameSamples.Count/2]:F2}; p95Ms={frameSamples[(int)(frameSamples.Count*.95)]:F2}; meanMs={total/frameSamples.Count:F2}; meanFPS={1000*frameSamples.Count/total:F1}. Includes editor/UI scheduling; not standalone GPU/CPU profiling.\n");
                frameSamples.Clear();
            }
            using var grids = em.CreateEntityQuery(typeof(GridComponent), typeof(GridTerrain));
            var entity = grids.GetSingletonEntity(); var grid = em.GetComponentData<GridComponent>(entity);
            var data = em.GetBuffer<GridTerrain>(entity);
            using var terrainCopy = data.ToNativeArray(Allocator.Temp);
            var map = new GeneratedTerrain { grid = grid, cells = terrainCopy.ToArray(), spawns = Array.Empty<int2>() };
            var spawnBuffer = em.GetBuffer<GridSpawnCell>(entity); map.spawns = new int2[spawnBuffer.Length];
            for (int i = 0; i < spawnBuffer.Length; i++) map.spawns[i] = spawnBuffer[i].cell;
            var config = AssetDatabase.LoadAssetAtPath<MapGenConfig>("Assets/_Project/Tests/Fixtures/MapGeneration/MapGenConfig.asset");
            if (grid.width != (int)config.MapSize || map.spawns.Length != config.Terrain.players) throw new Exception("Baked Main grid differs from config size/player count.");
            using var resources = em.CreateEntityQuery(typeof(MapResourceIdentity), typeof(BlockageData), typeof(Unity.Transforms.LocalTransform));
            using var nodes = resources.ToEntityArray(Allocator.Temp);
            foreach (var node in nodes)
            {
                var position = em.GetComponentData<Unity.Transforms.LocalTransform>(node).Position;
                if (!PlayerSpawnPlacement.TryFootprint(grid, position, em.GetComponentData<BlockageData>(node).LocalRect, out var min, out var max)) throw new Exception("Resource outside grid.");
                for (int y = math.max(0, min.y - 1); y <= math.min(grid.height - 1, max.y + 1); y++)
                    for (int x = math.max(0, min.x - 1); x <= math.min(grid.width - 1, max.x + 1); x++)
                        if (map.cells[GridHelper.GetNodeIndex(new int2(x,y),grid)].RampId >= 0) throw new Exception("Main resource touches ramp margin.");
            }
            CaptureMain(map, renderer, pending, nodes.Length);
            SessionState.SetString(Pending, ""); EditorApplication.isPlaying = false;
        }
        catch (Exception e)
        {
            File.WriteAllText(Folder + "/failure.txt", e.ToString()); UnityEngine.Debug.LogException(e);
            SessionState.SetString(Pending, ""); EditorApplication.isPlaying = false;
        }
    }

    public static string Signature(GeneratedTerrain map)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        foreach (var c in map.cells) { writer.Write(c.heightLevel); writer.Write(c.walkable); writer.Write(c.isMountain); writer.Write(c.isCliff); writer.Write(c.RampId); writer.Write(c.RampDirection); writer.Write(c.UsesVertexCorners); writer.Write(c.CornerMask); }
        writer.Flush(); using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-", "");
    }
    static void CaptureMain(GeneratedTerrain map, TerrainChunkRenderer renderer, string stage, int resources)
    {
        string folder = Folder + "/" + stage; Directory.CreateDirectory(folder);
        const string expectedHash="77764F5FF38714982D0B5D6E71B39364FA95D032B01ADBBE4044C924623A60B2";
        if(Signature(map)!=expectedHash||resources!=2040)throw new Exception("Current art review map/resource fixture changed; review fixture before accepting.");
        var camera = Camera.main; var oldPos = camera.transform.position; float oldSize = camera.orthographicSize;
        try { Shots(map, camera, folder); }
        finally { camera.transform.position = oldPos; camera.orthographicSize = oldSize; }
        var atlas = (Texture2D)renderer.PresentationRoot.GetComponentInChildren<MeshRenderer>().sharedMaterial.GetTexture("_BaseMap");
        File.WriteAllText(folder + "/report.txt", $"PASS Main Ready; grid={map.grid.width}x{map.grid.height}; players={map.spawns.Length}; resources={resources}; all resource footprints outside one-cell ramp margin; terrainHash={Signature(map)}; batches={renderer.PresentationRoot.GetComponentsInChildren<MeshRenderer>().Length}; atlas={atlas.width}x{atlas.height}; atlasRGBABytes={atlas.width * atlas.height * 4L}; captures exclude screen-space UI.\n");
    }

    public static void Shots(GeneratedTerrain map, Camera camera, string folder)
    {
        var features = new Dictionary<string,int>();
        if (map.spawns.Length > 0) features["spawn"] = GridHelper.GetNodeIndex(map.spawns[0],map.grid);
        int middle = GridHelper.GetNodeIndex(new int2(map.grid.width/2,map.grid.height/2),map.grid);
        features["flat"] = features.ContainsKey("spawn") ? features["spawn"] : middle;
        float nearest = float.MaxValue; int ramp = -1, mountain = -1, cliff = -1;
        for(int i=0;i<map.cells.Length;i++)
        {
            var cell = map.cells[i]; var p = GridHelper.GetGridPosFromIndex(i,map.grid);
            float d = math.distancesq((float2)p,new float2(map.grid.width/2,map.grid.height/2));
            if(cell.RampId>=0 && d<nearest) { nearest=d; ramp=i; }
            if(cell.isMountain && mountain<0) mountain=i;
            if(cell.isCliff && cell.RampId<0 && cliff<0) cliff=i;
        }
        if(ramp>=0)features["ramp"]=ramp; if(mountain>=0)features["mountain"]=mountain; if(cliff>=0)features["cliff"]=cliff;
        var cameraSetup = camera.GetComponent<IsometricTerrainCamera>(); float oldSetupSize = cameraSetup ? cameraSetup.size : 0;
        try
        {
            foreach(var feature in features) foreach(float zoom in new[]{8f,18f,40f})
            {
                var target = (Vector3)GridHelper.GridToWorld(GridHelper.GetGridPosFromIndex(feature.Value,map.grid),map.grid);
                if(TerrainVisualSurface.Active!=null) target.y=TerrainVisualSurface.Active.Sample(target.x,target.z);
                camera.transform.position=target-camera.transform.forward*80; camera.orthographicSize=zoom;
                if(cameraSetup)cameraSetup.size=zoom;
                Capture(camera,folder+"/"+feature.Key+"-"+zoom+".png");
            }
        }
        finally { if(cameraSetup)cameraSetup.size=oldSetupSize; }
    }
    public static void Capture(Camera camera,string path)
    {
        var target = new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB); target.Create();
        var texture = new Texture2D(1280,720,TextureFormat.RGBA32,false); var old = RenderTexture.active;
        try
        {
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            RenderTexture.active=target; texture.ReadPixels(new Rect(0,0,1280,720),0,0); texture.Apply();
            File.WriteAllBytes(path,texture.EncodeToPNG());
        }
        finally { RenderTexture.active=old; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(texture); }
    }
}
