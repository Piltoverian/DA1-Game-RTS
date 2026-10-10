using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class CanonicalTerrainValidation
{
    public static void RunCleanupSuite()
    {
        if(Application.isBatchMode)UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/Main.unity");
        StockIsometricSpriteValidation.ValidateOffset();
        TerrainPresentationValidation.RunMountainIntegration();
        BootstrapSpawnSmokeTest.TestResourcePlanner();
        if(!File.ReadAllText("Artifacts/SpawnSmoke/resource-planner.txt").StartsWith("PASS"))throw new Exception("Resource planner regression failed; inspect its report");
        RunStock();
        Debug.Log("TERRAIN_CLEANUP_SUITE_PASS");
    }
    [MenuItem("RTS/Tests/Validate Stock Reference Sprites")]
    public static void RunStock()
    {
        StockIsometricSpriteValidation.ValidateSpriteScale();
        StockIsometricSpriteValidation.ValidateNamedSpriteSlots();
        RunWithTheme(StockIsometricSpriteValidation.LoadTheme());
    }
    [MenuItem("RTS/Tests/Validate Selected Terrain Theme")]
    public static void RunSelected()
    {
        var theme=Selection.activeObject as TerrainTheme;
        if(!theme)throw new Exception("Select a TerrainTheme asset in Project first");
        RunWithTheme(theme);
    }
    public static void RunWithTheme(TerrainTheme source)
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play before terrain validation");
        const string folder="Artifacts/TerrainValidation/Canonical";
        Directory.CreateDirectory(folder);
        var previous=TerrainVisualSurface.Active;
        var owner=new GameObject("Canonical terrain validation owner");
        var cameraObject=new GameObject("Canonical terrain validation camera");
        TerrainChunkRenderer renderer=null;
        TerrainTheme theme=null;
        RenderTexture target=null;
        Texture2D image=null;
        try
        {
            theme=UnityEngine.Object.Instantiate(source);
            // A finished library must render without offline source artwork/UVs.
            theme.groundArtwork=null;theme.rampArtwork=null;theme.cliffArtwork=null;
            theme.groundSpriteCorners=null;theme.rampSpriteCorners=null;theme.cliffSpriteCorners=null;
            if(!theme.stockUnlitShader||ShaderUtil.ShaderHasError(theme.stockUnlitShader))throw new Exception("Invalid stock Unlit shader");
            var grid=new GridComponent{width=64,height=64,cellsize=1,origin=Vector3.zero};
            var settings=new TerrainGenerationSettings{algorithm=TerrainGenerationAlgorithm.SharedVertexSlopes,seed=30000,players=2,protectedRadius=3,landscapeHeight=3,detailAmplitude=.3f,wavelength=12,levelStep=1,cliffCells=1,rampWidth=3};
            var map=TerrainGeneration.Generate(grid,settings);
            TerrainVisualCompiler.Compile(map);
            var camera=cameraObject.AddComponent<Camera>();
            camera.enabled=false;camera.orthographic=true;camera.orthographicSize=42;camera.aspect=1;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.04f,.07f,.09f);
            camera.cullingMask=1<<31;camera.farClipPlane=300;
            camera.transform.rotation=Quaternion.Euler(TerrainTheme.CameraPitch,TerrainTheme.CameraYaw,0);
            camera.transform.position=new Vector3(32,0,32)-camera.transform.forward*100;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            owner.transform.SetPositionAndRotation(new Vector3(7,9,-3),Quaternion.Euler(12,22,4));
            renderer=owner.AddComponent<TerrainChunkRenderer>();
            renderer.Build(map,theme,camera);
            if(renderer.PresentationRoot.transform.parent)throw new Exception("Projected root must have identity world transform");
            int vertices=0,walls=0,ramps=0,mountains=0,batches=0,priorOrder=int.MinValue;
            foreach(var cell in map.cells)if(cell.isMountain)mountains++;
            foreach(var filter in renderer.PresentationRoot.GetComponentsInChildren<MeshFilter>())
            {
                filter.gameObject.layer=31;
                var mesh=filter.sharedMesh;var values=mesh.vertices;var colors=mesh.colors;
                var material=filter.GetComponent<MeshRenderer>().sharedMaterial;
                int order=filter.GetComponent<MeshRenderer>().sortingOrder;
                if(order<=priorOrder)throw new Exception("Global painter order lost at batch boundary");
                priorOrder=order;batches++;
                if(material.shader.name!="Universal Render Pipeline/Unlit"||material.GetFloat("_ZWrite")!=0)throw new Exception("Incorrect terrain material/depth mode");
                for(int i=0;i<values.Length;i++)
                {
                    if(values[i].y!=0||filter.transform.TransformPoint(values[i]).y!=0)throw new Exception("Terrain vertex local/world Y is not exactly zero");
                    vertices++;if(colors[i].a<.5f)walls++;if(colors[i].a>1.5f)ramps++;
                }
                for(int i=0;i<values.Length;i+=4)
                    if(Mathf.Abs(Vector3.Dot(camera.transform.right,values[i+3]-values[i]))>.001f||Mathf.Abs(Vector3.Dot(camera.transform.up,values[i+1]-values[i]))>.001f)
                        throw new Exception("Sprite rectangle warped in screen space");
            }
            if(vertices==0||walls==0||ramps==0||mountains==0)throw new Exception("Fixture missing terrain/cliff/ramp/mountain");
            TerrainPresentationValidation.Validate(map,TerrainVisualSurface.Active,folder);
            target=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);target.Create();
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            var old=RenderTexture.active;
            image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            try{RenderTexture.active=target;image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();}
            finally{RenderTexture.active=old;}
            int visible=0;foreach(var pixel in image.GetPixels32())if(pixel.g>40)visible++;
            if(visible<1000)throw new Exception("Canonical terrain capture contains too few visible pixels");
            File.WriteAllBytes(folder+"/terrain.png",image.EncodeToPNG());
            var atlas=(Texture2D)renderer.PresentationRoot.GetComponentInChildren<MeshRenderer>().sharedMaterial.GetTexture("_BaseMap");
            File.WriteAllBytes(folder+"/atlas.png",atlas.EncodeToPNG());
            renderer.Clear();
            if(renderer.PresentationRoot||TerrainVisualSurface.Active!=null)throw new Exception("Renderer clear leaked root/active surface");
            renderer.Build(map,theme,camera);
            string result=$"PASS canonical-only terrain: theme={source.name}; {vertices} exact local/world Y0 vertices, {batches} ordered batches, {walls} wall/{ramps} ramp vertices, {mountains} mountain cells; offline sources absent; picking/ECS separation and Clear/rebuild passed. No 3D occlusion parity or FPS assertion.";
            File.WriteAllText(folder+"/render.txt",result);Debug.Log("TERRAIN_CANONICAL_PASS: "+result);
        }
        finally
        {
            if(renderer)renderer.Clear();
            UnityEngine.Object.DestroyImmediate(owner);UnityEngine.Object.DestroyImmediate(cameraObject);
            if(theme)UnityEngine.Object.DestroyImmediate(theme);
            if(image)UnityEngine.Object.DestroyImmediate(image);
            if(target){target.Release();UnityEngine.Object.DestroyImmediate(target);}
            previous?.Activate();
        }
    }
}
