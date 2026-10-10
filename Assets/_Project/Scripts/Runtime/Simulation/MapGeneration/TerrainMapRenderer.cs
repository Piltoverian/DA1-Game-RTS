using System;
using Unity.Mathematics;
using UnityEngine;
using Unity.Entities;

// Reads the authoritative baked grid; only the canonical sprite renderer is available.
public class TerrainMapRenderer : MonoBehaviour
{
    public TerrainTheme theme;
    [Tooltip("Frame a nearby cliff/ramp/mountain area after initial map load.")]
    public bool frameRampOnLoad = true;
    TerrainChunkRenderer terrainRenderer;

    void Update()
    {
        if (terrainRenderer) return;
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;
        using var query = world.EntityManager.CreateEntityQuery(typeof(GridComponent), typeof(GridTerrain));
        if (query.IsEmptyIgnoreFilter) return;
        var entity = query.GetSingletonEntity();
        var grid = world.EntityManager.GetComponentData<GridComponent>(entity);
        var buffer = world.EntityManager.GetBuffer<GridTerrain>(entity);
        if (buffer.Length != grid.width * grid.height) return;
        var camera = Camera.main;
        if (!camera) return;
        var map = new GeneratedTerrain { grid = grid, cells = new GridTerrain[buffer.Length], spawns = Array.Empty<int2>() };
        for (int i = 0; i < buffer.Length; i++) map.cells[i] = buffer[i];
        if(world.EntityManager.HasBuffer<GridSpawnCell>(entity)){
            var spawns=world.EntityManager.GetBuffer<GridSpawnCell>(entity);map.spawns=new int2[spawns.Length];
            for(int i=0;i<spawns.Length;i++)map.spawns[i]=spawns[i].cell;
        }
        try
        {
            if (!theme) throw new InvalidOperationException("Assign TerrainMapRenderer.theme");
            TerrainVisualCompiler.Compile(map);
            IsometricTerrainCamera.Configure(camera);
            terrainRenderer = gameObject.AddComponent<TerrainChunkRenderer>();
            terrainRenderer.Build(map, theme, camera);
            if (frameRampOnLoad) IsometricTerrainCamera.FrameNearestRamp(map, camera);
        }
        catch { Clear(); enabled = false; throw; }
    }
    void Clear()
    {
        if (!terrainRenderer) return;
        terrainRenderer.Clear();
        if (Application.isPlaying) Destroy(terrainRenderer); else DestroyImmediate(terrainRenderer);
        terrainRenderer = null;
    }
    void OnDestroy() { Clear(); }
    void OnDisable() { Clear(); }
}
