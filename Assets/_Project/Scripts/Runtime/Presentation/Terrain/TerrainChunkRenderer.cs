using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// One canonical sprite pipeline. All retained terrain vertices remain local/world Y=0.
public class TerrainChunkRenderer : MonoBehaviour
{
    [Serializable] public sealed class BuildProfile
    {
        public double clearMs,textureCollectMs,atlasPackMs,materialMs,surfaceMs,meshMs,verifyMs,totalMs;
        public int sourceTextures,atlasWidth,atlasHeight,vertices,batches;
        public long atlasRuntimeBytes;
    }
    [NonSerialized] public bool MeasureBuild;
    public BuildProfile LastBuildProfile { get; private set; }
    GameObject root;
    Texture2D atlas;
    Texture2D geometryAtlas;
    Material groundMaterial;
    TerrainVisualSurface surface;
    Camera view;
    Quaternion viewRotation;
    bool warned;
    readonly List<Mesh> meshes = new List<Mesh>();
    public GameObject PresentationRoot => root;

    public void Build(GeneratedTerrain map, TerrainTheme theme, Camera camera, Unity.Entities.World presentationWorld=null, IReadOnlyDictionary<int,GridTerrain> known=null)
    {
        var profile=MeasureBuild?new BuildProfile():null;
        long start=profile==null?0:System.Diagnostics.Stopwatch.GetTimestamp(),stamp=start;
        void Mark(ref double value){long now=System.Diagnostics.Stopwatch.GetTimestamp();value=(now-stamp)*1000.0/System.Diagnostics.Stopwatch.Frequency;stamp=System.Diagnostics.Stopwatch.GetTimestamp();}
        Clear();
        if(profile!=null)Mark(ref profile.clearMs);
        try
        {
            if (!theme || !camera || !camera.orthographic)
                throw new InvalidOperationException("Assign a terrain theme and orthographic isometric camera");
            if (Mathf.Abs(camera.transform.right.y) > .001f)
                throw new InvalidOperationException("Canonical camera requires zero roll");
            float rise = theme.ResolveTierRise(map.grid.cellsize, camera.transform.forward);
            if(!theme.IsRenderReady(out string reason))throw new InvalidOperationException(reason);

            var textures = new List<Texture2D>();
            var ids = new Dictionary<Texture2D, int>();
            void Add(Texture2D texture, bool tile)
            {
                if (!texture || !texture.isReadable)
                    throw new InvalidOperationException("Terrain PNG slots must be assigned and Read/Write enabled");
                if (tile && (texture.width != TerrainTheme.CanvasPixels || texture.height != TerrainTheme.CanvasPixels))
                    throw new InvalidOperationException("Top/wall PNG canvas must be 128x128; do not crop sprites");
                if (!ids.ContainsKey(texture)) { ids.Add(texture, textures.Count); textures.Add(texture); }
            }
            for (int mask = 0; mask < 16; mask++) Add(theme.GetTopSprite(mask), true);
            for (int side = 0; side < 2; side++)
                for (int mask = 0; mask < 16; mask++)
                    if (TerrainTheme.IsVisibleWallMask(mask)) Add(theme.GetWallSprite(side, mask), true);
            if(theme.mountainCaps!=null&&theme.mountainCaps.Length>0){
                if(theme.mountainCaps.Length!=16)throw new InvalidOperationException("Mountain caps require all 16 masks");
                foreach(var texture in theme.mountainCaps)
                {
                    if(!texture||texture.width!=128||texture.height<128||texture.height>224)throw new InvalidOperationException("Mountain cap canvas must be 128 wide and 128..224 high; reimport its material atlas");
                    if(texture.height!=theme.mountainCaps[0].height)throw new InvalidOperationException("All 16 mountain caps must have the same canvas height; reconcile manual overrides before rendering");
                    Add(texture,false);
                }
            }
            if(profile!=null){profile.sourceTextures=textures.Count;Mark(ref profile.textureCollectMs);}

            atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                { name = "Canonical terrain atlas", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var rects = atlas.PackTextures(textures.ToArray(), 4, 2048, false);
            for(int i=0;i<textures.Count;i++)
                if(Mathf.Abs(rects[i].width*atlas.width-textures[i].width)>1 || Mathf.Abs(rects[i].height*atlas.height-textures[i].height)>1)
                    throw new InvalidOperationException("Atlas packing scaled terrain art; reduce the texture bank before promotion");
            if(profile!=null){profile.atlasWidth=atlas.width;profile.atlasHeight=atlas.height;Mark(ref profile.atlasPackMs);}
            groundMaterial = new Material(theme.materialAtlasShader) { name = "Terrain material atlas" };
            groundMaterial.SetTexture("_BaseMap", atlas);
            {
                var geometry=new Dictionary<Texture2D,Texture2D>();
                foreach(var entry in theme.spriteDepths)if(entry.Color&&entry.Geometry)geometry[entry.Color]=entry.Geometry;
                geometryAtlas=new Texture2D(atlas.width,atlas.height,TextureFormat.RGBA32,false,true){name="Terrain geometric depth atlas",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                foreach(var texture in textures)
                {
                    if(!geometry.TryGetValue(texture,out var depth)||!depth.isReadable||depth.width!=texture.width||depth.height!=texture.height)
                        throw new InvalidOperationException("Missing imported geometric depth for "+texture.name+"; reconcile manual color and geometry or reimport");
                    var rect=rects[ids[texture]];geometryAtlas.SetPixels(Mathf.RoundToInt(rect.x*atlas.width),Mathf.RoundToInt(rect.y*atlas.height),texture.width,texture.height,depth.GetPixels());
                }
                geometryAtlas.Apply();groundMaterial.SetTexture("_GeometryMap",geometryAtlas);groundMaterial.SetTexture("_SourceAtlas",theme.sourceMaterialAtlas);
                groundMaterial.SetVector("_Grid",new Vector4(map.grid.origin.x,map.grid.origin.z,map.grid.cellsize,theme.groundRegionCells));
                groundMaterial.SetVector("_ViewForward",camera.transform.forward);groundMaterial.SetFloat("_VisualSeed",theme.groundVisualSeed);
                groundMaterial.SetFloat("_FadeOpacity",theme.mountainFadeOpacity);groundMaterial.renderQueue=(int)RenderQueue.AlphaTest;
            }
            if(profile!=null)Mark(ref profile.materialMs);

            surface = new TerrainVisualSurface(map, rise);
            if(profile!=null)Mark(ref profile.surfaceMs);
            root = CanonicalIsometricSprites.Build(map, theme, surface, camera.transform.forward, groundMaterial, ids, rects, meshes,known);
            root.AddComponent<TerrainMountainFade>().Initialize(groundMaterial,camera,surface,theme.mountainFadeRadiusCells,presentationWorld);
            if(profile!=null)Mark(ref profile.meshMs);
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
                foreach (var vertex in filter.sharedMesh.vertices)
                    if (vertex.y != 0 || filter.transform.TransformPoint(vertex).y != 0)
                        throw new InvalidOperationException("Terrain vertices must remain exactly local/world Y=0");
            view = camera;
            viewRotation = camera.transform.rotation;
            surface.Activate();
            if(profile!=null){Mark(ref profile.verifyMs);profile.totalMs=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;
                profile.batches=meshes.Count;foreach(var mesh in meshes)profile.vertices+=mesh.vertexCount;
                profile.atlasRuntimeBytes=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(atlas);
            }
            LastBuildProfile=profile;
        }
        catch { Clear(); throw; }
    }

    void LateUpdate()
    {
        if (!view || !root) return;
        if (Quaternion.Angle(view.transform.rotation, viewRotation) > .01f || !view.orthographic)
        {
            view.transform.rotation = viewRotation;
            view.orthographic = true;
            if (!warned) { Debug.LogWarning("Canonical terrain keeps camera angle fixed; pan and orthographic zoom remain available."); warned = true; }
        }
    }
    public void SetFog(Texture2D mask)
    {
        if(!groundMaterial)return;
        groundMaterial.SetTexture("_FogMask",mask);
        groundMaterial.SetFloat("_FogEnabled",mask?1:0);
        if(mask)groundMaterial.SetVector("_FogSize",new Vector4(mask.width,mask.height,0,0));
    }
    void OnEnable() { if (root) root.SetActive(true); surface?.Activate(); }
    void OnDisable() { if (root) root.SetActive(false); surface?.Deactivate(); }
    static void Release(UnityEngine.Object value)
    {
        if (!value) return;
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }
    public void Clear()
    {
        surface?.Deactivate(); surface = null;
        if (root) root.SetActive(false);
        Release(root); root = null;
        foreach (var mesh in meshes) Release(mesh);
        meshes.Clear();
        Release(groundMaterial); groundMaterial = null;
        Release(atlas); atlas = null;
        Release(geometryAtlas);geometryAtlas=null;
        view = null; warned = false;
    }
    void OnDestroy() { Clear(); }
}

