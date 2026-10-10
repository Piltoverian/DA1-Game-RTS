using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// One canonical sprite pipeline. All retained terrain vertices remain local/world Y=0.
public class TerrainChunkRenderer : MonoBehaviour
{
    GameObject root;
    Texture2D atlas;
    Material groundMaterial;
    TerrainVisualSurface surface;
    Camera view;
    Quaternion viewRotation;
    bool warned;
    readonly List<Mesh> meshes = new List<Mesh>();
    public GameObject PresentationRoot => root;

    public void Build(GeneratedTerrain map, TerrainTheme theme, Camera camera)
    {
        Clear();
        try
        {
            if (!theme || !camera || !camera.orthographic)
                throw new InvalidOperationException("Assign a terrain theme and orthographic isometric camera");
            if (!theme.stockUnlitShader || theme.stockUnlitShader.name != "Universal Render Pipeline/Unlit")
                throw new InvalidOperationException("Terrain requires stock Universal Render Pipeline/Unlit");
            if (Mathf.Abs(camera.transform.right.y) > .001f)
                throw new InvalidOperationException("Canonical camera requires zero roll");
            float rise = theme.ResolveTierRise(map.grid.cellsize, camera.transform.forward);

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
            if (theme.flatGroundVariants != null)
                foreach (var variant in theme.flatGroundVariants) if (variant) Add(variant, true);
            for (int side = 0; side < 2; side++)
                for (int mask = 0; mask < 16; mask++)
                    if (TerrainTheme.IsVisibleWallMask(mask)) Add(theme.GetWallSprite(side, mask), true);
            if(theme.mountainCaps!=null&&theme.mountainCaps.Length>0){
                if(theme.mountainCaps.Length!=16)throw new InvalidOperationException("Mountain caps require all 16 masks");
                foreach(var texture in theme.mountainCaps)Add(texture,true);
            }
            else if (theme.mountainArtwork) Add(theme.mountainArtwork, false);
            if(theme.fringeDecorations!=null)foreach(var texture in theme.fringeDecorations)Add(texture,true);

            atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                { name = "Canonical terrain atlas", filterMode = theme.smoothTextureSampling ? FilterMode.Bilinear : FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var rects = atlas.PackTextures(textures.ToArray(), theme.smoothTextureSampling ? 4 : 2, 2048, false);
            for(int i=0;i<textures.Count;i++)
                if(Mathf.Abs(rects[i].width*atlas.width-textures[i].width)>1 || Mathf.Abs(rects[i].height*atlas.height-textures[i].height)>1)
                    throw new InvalidOperationException("Atlas packing scaled terrain art; reduce the texture bank before promotion");
            groundMaterial = new Material(theme.stockUnlitShader) { name = "Canonical terrain Unlit" };
            groundMaterial.SetTexture("_BaseMap", atlas);
            groundMaterial.SetColor("_BaseColor", Color.white);
            groundMaterial.SetFloat("_AlphaClip", 1);
            groundMaterial.SetFloat("_Cutoff", .1f);
            groundMaterial.EnableKeyword("_ALPHATEST_ON");
            // Draw after skybox without writing the depth of the logical plane.
            groundMaterial.SetFloat("_Cull", 0);
            groundMaterial.SetFloat("_ZWrite", 0);
            groundMaterial.renderQueue = (int)RenderQueue.Transparent - 100;
            groundMaterial.SetShaderPassEnabled("ShadowCaster", false);
            groundMaterial.SetShaderPassEnabled("DepthOnly", false);
            groundMaterial.SetShaderPassEnabled("DepthNormalsOnly", false);

            surface = new TerrainVisualSurface(map, rise);
            root = CanonicalIsometricSprites.Build(map, theme, surface, camera.transform.forward, groundMaterial, ids, rects, meshes);
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
                foreach (var vertex in filter.sharedMesh.vertices)
                    if (vertex.y != 0 || filter.transform.TransformPoint(vertex).y != 0)
                        throw new InvalidOperationException("Terrain vertices must remain exactly local/world Y=0");
            view = camera;
            viewRotation = camera.transform.rotation;
            surface.Activate();
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
        view = null; warned = false;
    }
    void OnDestroy() { Clear(); }
}
