using System;
using System.Collections.Generic;
using UnityEngine;

// Player-authorized render input. Transport headers and simulation buffers stay with their producers.
public sealed class TerrainView : IDisposable
{
    public bool Ready;
    public GridComponent Grid;
    public TerrainSyncSettings Settings;
    public uint GeometryVersion;
    public Dictionary<int, TerrainCellVisualData> Cells = new();
    public Texture2D Mask;
    Color32[] pixels;
    readonly HashSet<int> visible = new();

    public void SetGeometry(GridComponent grid, TerrainSyncSettings settings, Dictionary<int, TerrainCellVisualData> cells)
    {
        Grid = grid;
        Settings = settings;
        Cells = cells;
        GeometryVersion++;
        Ready = true;
    }

    public void UpdateVisibility(IEnumerable<int> visibleCells)
    {
        visible.Clear();
        foreach (int cell in visibleCells) visible.Add(cell);
        bool changed = false;
        if (!Mask || Mask.width != Grid.width || Mask.height != Grid.height)
        {
            Release(Mask);
            Mask = new Texture2D(Grid.width, Grid.height, TextureFormat.RGBA32, false, true)
            { name = "Player terrain FOW", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            pixels = new Color32[checked(Grid.width * Grid.height)];
            changed = true;
        }
        for (int i = 0; i < pixels.Length; i++)
        {
            byte shade = Cells.ContainsKey(i) ? (visible.Contains(i) ? (byte)255 : (byte)80) : (byte)0;
            var color = new Color32(shade, shade, shade, shade == 0 ? (byte)0 : (byte)255);
            if (pixels[i].Equals(color)) continue;
            pixels[i] = color;
            changed = true;
        }
        if (changed) { Mask.SetPixels32(pixels); Mask.Apply(false); }
    }

    public void Dispose()
    {
        Ready = false;
        Cells.Clear();
        GeometryVersion++;
        Release(Mask);
        Mask = null;
        pixels = null;
        visible.Clear();
    }

    static void Release(UnityEngine.Object value)
    {
        if (!value) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value);
    }
}
