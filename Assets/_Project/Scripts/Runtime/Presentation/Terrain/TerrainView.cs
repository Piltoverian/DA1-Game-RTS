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
    readonly HashSet<int> nextVisible = new();
    readonly HashSet<int> dirtyMask = new();
    public bool IsCellVisible(int cellIndex) => visible.Contains(cellIndex);

    public void SetGeometry(GridComponent grid, TerrainSyncSettings settings, Dictionary<int, TerrainCellVisualData> cells)
    {
        if (Grid.width != grid.width || Grid.height != grid.height || Grid.cellsize != grid.cellsize || !Grid.origin.Equals(grid.origin))
        { Release(Mask); Mask=null; pixels=null; visible.Clear(); dirtyMask.Clear(); }
        else
        {
            foreach(int index in Cells.Keys) if(!cells.ContainsKey(index)) dirtyMask.Add(index);
            foreach(int index in cells.Keys) if(!Cells.ContainsKey(index)) dirtyMask.Add(index);
        }
        Grid = grid;
        Settings = settings;
        Cells = cells;
        GeometryVersion++;
        Ready = true;
    }

    public void UpdateVisibility(IEnumerable<int> visibleCells)
    {
        nextVisible.Clear();
        foreach(int cell in visibleCells)nextVisible.Add(cell);
        foreach(int cell in visible)if(!nextVisible.Contains(cell))dirtyMask.Add(cell);
        foreach(int cell in nextVisible)if(!visible.Contains(cell))dirtyMask.Add(cell);
        visible.Clear(); foreach(int cell in nextVisible)visible.Add(cell);
        bool changed = false;
        if (!Mask || Mask.width != Grid.width || Mask.height != Grid.height)
        {
            Release(Mask);
            Mask = new Texture2D(Grid.width, Grid.height, TextureFormat.RGBA32, false, true)
            { name = "Player terrain FOW", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            pixels = new Color32[checked(Grid.width * Grid.height)];
            foreach(int cell in Cells.Keys)dirtyMask.Add(cell);
            changed = true;
        }
        foreach (int i in dirtyMask)
        {
            if(i<0||i>=pixels.Length)continue;
            byte shade = Cells.ContainsKey(i) ? (visible.Contains(i) ? (byte)255 : (byte)80) : (byte)0;
            var color = new Color32(shade, shade, shade, shade == 0 ? (byte)0 : (byte)255);
            if (pixels[i].Equals(color)) continue;
            pixels[i] = color;
            changed = true;
        }
        dirtyMask.Clear();
        if (changed) { Mask.SetPixels32(pixels); Mask.Apply(false); }
    }

    public void Dispose()
    {
        Ready = false;
        Cells = new Dictionary<int,TerrainCellVisualData>();
        GeometryVersion++;
        Release(Mask);
        Mask = null;
        pixels = null;
        visible.Clear(); nextVisible.Clear(); dirtyMask.Clear();
    }

    static void Release(UnityEngine.Object value)
    {
        if (!value) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value);
    }
}
