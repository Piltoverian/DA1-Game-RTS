using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public static class TerrainStagingChecks
{
    [MenuItem("Tools/RTS/Check Staged Terrain Presentation")]
    public static void Run()
    {
        var theme = LoadTheme();
        var originalSurface = TerrainVisualSurface.Active;
        using var world = new World("Staged terrain checks", WorldFlags.Game);
        var owner = new GameObject("Staged terrain check owner");
        var cameraOwner = new GameObject("Staged terrain check camera");
        var renderers = new List<TerrainChunkRenderer>();
        var view = new TerrainView();
        IEnumerator steps = null;
        int checks = 0;
        try
        {
            var camera = cameraOwner.AddComponent<Camera>();
            camera.orthographic = true;
            camera.transform.rotation = Quaternion.Euler(30, 45, 0);
            var grid = new GridComponent { width = 64, height = 48, cellsize = 1, initialized = true };
            var initialCells = Cells(2, false);
            var expandedCells = Cells(grid.width * grid.height, false);
            var old = AddRenderer();
            old.Build(grid, theme, camera, world, initialCells);
            var oldRoot = old.PresentationRoot;
            var oldSurface = TerrainVisualSurface.Active;
            var material = oldRoot.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            var atlas = material.GetTexture("_BaseMap");
            Check(oldRoot.activeSelf && oldSurface != null && material && atlas,
                "Synchronous Build must publish a complete initial image and picking surface.");

            var candidate = AddRenderer();
            steps = candidate.BuildInSteps(grid, theme, camera, world, expandedCells, old);
            Check(steps.MoveNext() && oldRoot.activeSelf && candidate.PresentationRoot == null
                && ReferenceEquals(TerrainVisualSurface.Active, oldSurface),
                "The first staging yield must retain the old image/surface and leave the candidate incomplete.");
            int yields = Drain(steps);
            (steps as IDisposable)?.Dispose(); steps = null;
            Check(yields >= 8 && candidate.PresentationRoot && !candidate.PresentationRoot.activeSelf
                && oldRoot.activeSelf && ReferenceEquals(TerrainVisualSurface.Active, oldSurface),
                "Collection, global sort and mesh batches must yield while the old image remains published.");
            candidate.Publish();
            var candidateSurface = TerrainVisualSurface.Active;
            old.Clear();
            Check(candidate.PresentationRoot.activeSelf && candidateSurface != null
                && !ReferenceEquals(candidateSurface, oldSurface)
                && ReferenceEquals(TerrainVisualSurface.Active, candidateSurface)
                && material && atlas
                && candidate.PresentationRoot.GetComponentInChildren<MeshRenderer>().sharedMaterial == material
                && material.GetTexture("_BaseMap") == atlas,
                "Clearing the replaced renderer must preserve the shared atlas/material and new picking surface.");

            var reverse = AddRenderer();
            steps = reverse.BuildInSteps(grid, theme, camera, world, Cells(grid.width * grid.height, true), candidate);
            Drain(steps); (steps as IDisposable)?.Dispose(); steps = null;
            reverse.Publish();
            CheckSameGeometry(candidate.PresentationRoot, reverse.PresentationRoot, grid, camera.transform.forward);
            reverse.Clear();
            candidate.Publish();
            Check(material && atlas && ReferenceEquals(TerrainVisualSurface.Active, candidateSurface),
                "Releasing another shared-art build must leave the retained candidate publishable.");

            view.SetGeometry(grid, default, initialCells);
            view.UpdateVisibility(new[] { 0 });
            var mask = view.Mask;
            Check(Shade(mask, 0) == 255 && Shade(mask, 1) == 80 && Shade(mask, 2) == 0,
                "Mask must distinguish visible, explored-hidden and unknown cells.");
            var rememberedCell = view.Cells[0];
            view.UpdateVisibility(Array.Empty<int>());
            Check(Shade(mask, 0) == 80 && ReferenceEquals(view.Cells[0], rememberedCell),
                "A hidden explored cell must keep its authorized geometry and shade 80.");
            var newlyExplored = Cells(3, false);
            view.SetGeometry(grid, default, newlyExplored);
            view.UpdateVisibility(new[] { 2 });
            Check(view.Mask == mask && Shade(mask, 0) == 80 && Shade(mask, 1) == 80
                && Shade(mask, 2) == 255 && Shade(mask, 3) == 0,
                "Adding an explored cell must refresh its mask pixel while preserving remembered/unknown pixels.");

            using (var retained = new LocalTerrainView())
            {
                retained.View.SetGeometry(grid, default, expandedCells);
                retained.View.UpdateVisibility(new[] { 0 });
                var retainedMask = retained.View.Mask;
                candidate.SetFog(retainedMask);
                Check(retained.Update(world.EntityManager, null) && retained.View.Mask == retainedMask && retainedMask,
                    "A missing local observation must retain the authorized fog texture instead of destroying the renderer's mask.");
                candidate.SetFog(view.Mask);
            }
            var terrainOwner = owner.AddComponent<TerrainMapRenderer>();
            terrainOwner.theme = theme;
            var cancelled = AddRenderer();
            var binding = new MapPresentationBinding
            {
                Owner = terrainOwner, Renderer = candidate, Candidate = cancelled, View = view,
                BuildSteps = cancelled.BuildInSteps(grid, theme, camera, world, expandedCells, candidate)
            };
            Check(binding.BuildSteps.MoveNext(), "Cancellation fixture must have a live staged build.");
            var entity = world.EntityManager.CreateEntity(typeof(MapPresentationState));
            world.EntityManager.SetComponentData(entity, new MapPresentationState { Phase = MapPresentationPhase.Ready, Revision = 7 });
            world.EntityManager.AddComponentObject(entity, binding);
            // The local source is temporarily absent: this exercises the system's waiting/cancel branch.
            world.GetOrCreateSystemManaged<MapTerrainPresentationSystem>().Update();
            var state = world.EntityManager.GetComponentData<MapPresentationState>(entity);
            Check(binding.BuildSteps == null && !binding.Candidate && binding.Renderer == candidate
                && candidate.PresentationRoot.activeSelf && material && atlas
                && ReferenceEquals(TerrainVisualSurface.Active, candidateSurface)
                && state.Phase == MapPresentationPhase.Ready && state.Revision == 7,
                "Transient geometry loss must cancel the candidate and preserve the last published image/surface.");
            binding.Dispose();
            world.EntityManager.DestroyEntity(entity);
            Check(TerrainVisualSurface.Active == null && !material && !atlas,
                "Final disposal must release the last shared-art reference and leave no active picking surface.");
            Debug.Log($"Staged terrain checks passed: {checks} assertions.");
        }
        finally
        {
            (steps as IDisposable)?.Dispose();
            foreach (var renderer in renderers) if (renderer) renderer.Clear();
            view.Dispose();
            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(cameraOwner);
            originalSurface?.Activate();
        }

        TerrainChunkRenderer AddRenderer()
        {
            var renderer = owner.AddComponent<TerrainChunkRenderer>();
            renderers.Add(renderer);
            return renderer;
        }

        void Check(bool condition, string message)
        {
            checks++;
            if (!condition) throw new InvalidOperationException(message);
        }

        void CheckSameGeometry(GameObject first, GameObject second, GridComponent grid, Vector3 forward)
        {
            var a = first.GetComponentsInChildren<MeshFilter>(true);
            var b = second.GetComponentsInChildren<MeshFilter>(true);
            Check(a.Length > 1 && a.Length == b.Length, "Sort fixture must cross a 2,048-face mesh batch boundary.");
            float previousDepth = float.PositiveInfinity;
            int totalFaces = 0;
            for (int i = 0; i < a.Length; i++)
            {
                var av = a[i].sharedMesh.vertices; var bv = b[i].sharedMesh.vertices;
                var auv = a[i].sharedMesh.uv; var buv = b[i].sharedMesh.uv;
                var acells = new List<Vector2>(); var bcells = new List<Vector2>();
                a[i].sharedMesh.GetUVs(2, acells); b[i].sharedMesh.GetUVs(2, bcells);
                bool same = av.Length == bv.Length && auv.Length == buv.Length && acells.Count == bcells.Count;
                if (same)
                    for (int j = 0; j < av.Length; j++)
                        same &= av[j].Equals(bv[j]) && auv[j].Equals(buv[j]) && acells[j].Equals(bcells[j]);
                Check(same, "Dictionary insertion order must not change canonical vertex, atlas UV or cell order.");
                bool flat = true, sorted = true;
                for (int j = 0; j < av.Length; j++)
                    flat &= av[j].y == 0 && a[i].transform.TransformPoint(av[j]).y == 0;
                for (int j = 0; j < acells.Count; j += 4)
                {
                    var cell = acells[j];
                    float depth = Vector3.Dot(new Vector3(grid.origin.x + cell.x * grid.cellsize, 0,
                        grid.origin.z + cell.y * grid.cellsize), forward);
                    sorted &= depth <= previousDepth;
                    previousDepth = depth; totalFaces++;
                }
                Check(flat && sorted, "All vertices must stay at Y=0 and depth order must remain global across batches.");
            }
            Check(totalFaces == grid.width * grid.height, "Every authorized flat cell must emit exactly one top face.");
        }
    }

    static int Drain(IEnumerator steps)
    {
        int yields = 0;
        while (steps.MoveNext())
            if (++yields > 10000) throw new InvalidOperationException("Staged terrain build did not complete within its guard.");
        return yields;
    }

    static byte Shade(Texture2D mask, int index) => mask.GetPixels32()[index].r;

    static Dictionary<int, TerrainCellVisualData> Cells(int count, bool reverse)
    {
        var cells = new Dictionary<int, TerrainCellVisualData>(count);
        for (int n = 0; n < count; n++)
        {
            int index = reverse ? count - n - 1 : n;
            cells.Add(index, new TerrainCellVisualData
            {
                CornerHeights = new float4(0), TopHeight = 0, TopMask = 0,
                Walls = Array.Empty<TerrainWallVisualData>()
            });
        }
        return cells;
    }

    static TerrainTheme LoadTheme()
    {
        foreach (string path in new[]
        {
            "Assets/_Project/Data/Terrain/Generated/MoonTerrain/LunarTheme.asset",
            "Assets/_Project/Data/Terrain/Generated/JadeSlateAtlas/Theme.asset"
        })
        {
            var theme = AssetDatabase.LoadAssetAtPath<TerrainTheme>(path);
            if (theme && theme.IsRenderReady(out _)) return theme;
        }
        throw new InvalidOperationException("Staging checks require a ready LunarTheme or JadeSlateAtlas theme asset.");
    }
}
