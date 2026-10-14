using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class TerrainAtlasPreview
{
    public static Texture2D Render(TerrainTheme source)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before rendering an atlas preview.");
        if (!source) throw new ArgumentNullException(nameof(source));
        if (!source.IsRenderReady(out string reason)) throw new InvalidOperationException(reason);
        var previousSurface = TerrainVisualSurface.Active;
        var previousTarget = RenderTexture.active;
        var owner = new GameObject("Terrain atlas preview") { hideFlags = HideFlags.HideAndDontSave };
        var cameraOwner = new GameObject("Terrain atlas preview camera") { hideFlags = HideFlags.HideAndDontSave };
        TerrainChunkRenderer renderer = null;
        TerrainTheme theme = null;
        RenderTexture target = null;
        Texture2D image = null;
        try
        {
            theme = UnityEngine.Object.Instantiate(source);
            var grid = new GridComponent { width = 64, height = 64, cellsize = 1, origin = Vector3.zero };
            var settings = new TerrainGenerationSettings { seed = 30000, players = 2, protectedRadius = 3, landscapeHeight = 3, detailAmplitude = .3f, wavelength = 12, levelStep = 1, cliffCells = 1, rampWidth = 3 };
            var map = TerrainGeneration.Generate(grid, settings);
            TerrainRampCompiler.Compile(map);
            var camera = cameraOwner.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 42;
            camera.aspect = 1;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.04f, .07f, .09f);
            camera.cullingMask = 1 << 31;
            camera.farClipPlane = 300;
            camera.transform.rotation = Quaternion.Euler(TerrainTheme.CameraPitch, TerrainTheme.CameraYaw, 0);
            camera.transform.position = new Vector3(32, 0, 32) - camera.transform.forward * 100;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            renderer = owner.AddComponent<TerrainChunkRenderer>();
            renderer.Build(map, theme, camera);
            foreach (var filter in renderer.PresentationRoot.GetComponentsInChildren<MeshFilter>()) filter.gameObject.layer = 31;
            target = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            target.Create();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            image = new Texture2D(512, 512, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
            image.Apply();
            return image;
        }
        catch
        {
            if (image) UnityEngine.Object.DestroyImmediate(image);
            throw;
        }
        finally
        {
            RenderTexture.active = previousTarget;
            if (renderer) renderer.Clear();
            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(cameraOwner);
            if (theme) UnityEngine.Object.DestroyImmediate(theme);
            if (target) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            previousSurface?.Activate();
        }
    }
}
