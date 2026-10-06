using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Entities;

// Renders the gameplay grid baked by GridAuthoring after its subscene loads.
// The child owns its meshes/materials; no per-cell GameObjects or gameplay Y changes.
public class TerrainMapRenderer : MonoBehaviour
{
    public TerrainTheme theme;
    public int chunkSize = 16;
    GameObject generatedRoot;
    readonly List<Mesh> ownedMeshes = new List<Mesh>();
    readonly List<Material> ownedMaterials = new List<Material>();
    void Update()
    {
        if (generatedRoot) return;
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;
        using var query = world.EntityManager.CreateEntityQuery(typeof(GridComponent), typeof(GridTerrain));
        if (query.IsEmptyIgnoreFilter) return; // Subscene loads asynchronously.
        var entity = query.GetSingletonEntity();
        var grid = world.EntityManager.GetComponentData<GridComponent>(entity);
        var buffer = world.EntityManager.GetBuffer<GridTerrain>(entity);
        if (buffer.Length != grid.width * grid.height) return;
        var candidate = new GeneratedTerrain { grid = grid, cells = new GridTerrain[buffer.Length], spawns = Array.Empty<int2>() };
        for (int i = 0; i < buffer.Length; i++) candidate.cells[i] = buffer[i];
        try { TerrainVisualCompiler.Compile(candidate); BuildTerrain(candidate); }
        catch { Clear(); enabled = false; throw; }
    }
    static void Release(UnityEngine.Object value) { if (!value) return; if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    void Clear()
    {
        if (generatedRoot) generatedRoot.SetActive(false);
        Release(generatedRoot); generatedRoot = null;
        foreach (var mesh in ownedMeshes) Release(mesh); ownedMeshes.Clear();
        foreach (var material in ownedMaterials) Release(material); ownedMaterials.Clear();
    }
    void OnDestroy() { Clear(); }
    void OnDisable() { Clear(); }
    void BuildTerrain(GeneratedTerrain candidate)
    {
        if (!theme || !theme.material)
            throw new InvalidOperationException("Assign a theme with material and nine terrain sprites");
        for (int tile = 0; tile < TerrainTheme.TileCount; tile++)
        {
            Sprite sprite = theme.GetSprite(tile);
            if (sprite.packed) throw new InvalidOperationException($"Terrain sprite '{sprite.name}' must use its individual texture; disable SpriteAtlas packing for this asset");
        }
        GridComponent grid = candidate.grid;
        Clear();
        generatedRoot = new GameObject("Generated terrain chunks"); generatedRoot.transform.SetParent(transform, false);
        generatedRoot.hideFlags = HideFlags.DontSave;
        var tileMaterials = new Material[TerrainTheme.TileCount];
        for (int tile = 0; tile < tileMaterials.Length; tile++)
        {
            Sprite sprite = theme.GetSprite(tile);
            var material = new Material(theme.material) { name = "Terrain " + sprite.name };
            material.mainTexture = sprite.texture;
            material.mainTextureScale = Vector2.one; material.mainTextureOffset = Vector2.zero;
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", sprite.texture);
                material.SetTextureScale("_BaseMap", Vector2.one); material.SetTextureOffset("_BaseMap", Vector2.zero);
            }
            tileMaterials[tile] = material; ownedMaterials.Add(material);
        }
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (!shader) throw new InvalidOperationException("URP Unlit shader missing");
        Material rockMaterial = new Material(shader); rockMaterial.SetColor("_BaseColor", theme.mountainColor); ownedMaterials.Add(rockMaterial);
        int side = Mathf.Clamp(chunkSize, 4, 32);
        var buckets = new Dictionary<int, List<TerrainTileDraw>>();
        int columns = (grid.width + side - 1) / side;
        foreach (var draw in candidate.draws)
        {
            int2 cell = GridHelper.GetGridPosFromIndex(draw.cell, grid);
            int key = GridHelper.GetNodeIndex(cell / side, columns);
            if (!buckets.TryGetValue(key, out var list)) buckets[key] = list = new List<TerrainTileDraw>(); list.Add(draw);
        }
        for (int by = 0; by < grid.height; by += side) for (int bx = 0; bx < grid.width; bx += side)
        {
            var tileVertices = new List<Vector3>(); var uv = new List<Vector2>();
            var tileTriangles = new List<int>[TerrainTheme.TileCount];
            for (int tile = 0; tile < TerrainTheme.TileCount; tile++) tileTriangles[tile] = new List<int>();
            var rockVertices = new List<Vector3>(); var rockTriangles = new List<int>();
            for (int y = by; y < Math.Min(by + side, grid.height); y++) for (int x = bx; x < Math.Min(bx + side, grid.width); x++)
            {
                int i = GridHelper.GetNodeIndex(new int2(x, y), grid);
                if (candidate.cells[i].isMountain) Quad(grid, i, 0, -1, 0, rockVertices, null, rockTriangles);
                else Quad(grid, i, 0, 7, 0, tileVertices, uv, tileTriangles[7]);
            }
            int key = GridHelper.GetNodeIndex(new int2(bx / side, by / side), columns);
            if (buckets.TryGetValue(key, out var draws))
                for (int k = 0; k < draws.Count; k++) { var d = draws[k]; Quad(grid, d.cell, grid.cellsize * (.001f * d.layer + .000001f * k), d.tile, d.rotation, tileVertices, uv, tileTriangles[d.tile]); }
            MakeSpriteChunk("Tiles " + bx + "," + by, tileVertices, uv, tileTriangles, tileMaterials);
            MakeChunk("Mountains " + bx + "," + by, rockVertices, null, rockTriangles, rockMaterial);
        }
    }
    void Quad(GridComponent g, int cell, float offset, int tile, int rotation, List<Vector3> vertices, List<Vector2> uv, List<int> triangles)
    {
        int2 gridPos = GridHelper.GetGridPosFromIndex(cell, g); float c = g.cellsize;
        Vector3 center = (Vector3)GridHelper.GridToWorld(gridPos, g); center.y += offset;
        Vector3[] world = { center + new Vector3(-c/2,0,-c/2), center + new Vector3(c/2,0,-c/2), center + new Vector3(c/2,0,c/2), center + new Vector3(-c/2,0,c/2) };
        int first = vertices.Count;
        for (int k = 0; k < 4; k++) vertices.Add(transform.InverseTransformPoint(world[k]));
        triangles.AddRange(new[] { first, first+2, first+1, first, first+3, first+2 });
        if (uv == null) return;
        Sprite sprite = theme.GetSprite(tile); Rect r = sprite.rect;
        float u0 = r.xMin / sprite.texture.width, u1 = r.xMax / sprite.texture.width;
        float v0 = r.yMin / sprite.texture.height, v1 = r.yMax / sprite.texture.height;
        Vector2[] corners = { new Vector2(u0,v1), new Vector2(u1,v1), new Vector2(u1,v0), new Vector2(u0,v0) };
        for (int k = 0; k < 4; k++) uv.Add(corners[(k - rotation + 4) % 4]);
    }
    void MakeSpriteChunk(string name, List<Vector3> vertices, List<Vector2> uv, List<int>[] triangles, Material[] materials)
    {
        if (vertices.Count == 0) return;
        var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 }; mesh.SetVertices(vertices); mesh.SetUVs(0, uv);
        var usedMaterials = new List<Material>();
        int count = 0; foreach (var indices in triangles) if (indices.Count > 0) count++;
        mesh.subMeshCount = count;
        int submesh = 0;
        for (int tile = 0; tile < triangles.Length; tile++) if (triangles[tile].Count > 0)
        { mesh.SetTriangles(triangles[tile], submesh++); usedMaterials.Add(materials[tile]); }
        mesh.RecalculateBounds(); ownedMeshes.Add(mesh);
        var go = new GameObject(name); go.transform.SetParent(generatedRoot.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterials = usedMaterials.ToArray();
    }
    void MakeChunk(string name, List<Vector3> vertices, List<Vector2> uv, List<int> triangles, Material material)
    {
        if (vertices.Count == 0) return;
        var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 }; mesh.SetVertices(vertices);
        if (uv != null) mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds(); ownedMeshes.Add(mesh);
        var go = new GameObject(name); go.transform.SetParent(generatedRoot.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = material;
    }
}
