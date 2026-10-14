using System;
using Unity.Mathematics;

// Geometry-only surface contract. No Camera, Texture, active renderer or UnityEngine object.
// Heights are presentation units; authoritative navigation continues to use XZ/cell topology.
public sealed class TerrainSurfaceData
{
    public readonly GeneratedTerrain Map;
    public readonly float Rise;
    readonly int minimum;
    readonly int[] ramps;
    public static float CanonicalRise(float cellSize) => cellSize * .25f * math.sqrt(2f) / math.cos(math.radians(30f));

    public TerrainSurfaceData(GeneratedTerrain map, float rise)
    {
        if (map == null || map.cells == null || map.grid.width <= 0 || map.grid.height <= 0
            || map.cells.Length != checked(map.grid.width * map.grid.height) || !(map.grid.cellsize > 0)
            || !math.isfinite(rise) || rise < 0) throw new ArgumentException("Invalid terrain surface data");
        Map = map; Rise = rise; minimum = int.MaxValue;
        foreach (var cell in map.cells) minimum = Math.Min(minimum, cell.heightLevel);
        ramps=map.rampFaceDirections;
        if(ramps==null||(ramps.Length!=0&&ramps.Length!=map.cells.Length))throw new ArgumentException("Compile ramp face directions before sampling terrain");
    }
    public bool IsRamp(int index) => Map.cells[index].UsesVertexCorners ? Map.cells[index].CornerMask != 0 : ramps.Length>0&&ramps[index] >= 0;
    public float CellHeight(int index, float u, float v)
    {
        var cell = Map.cells[index]; float high = (cell.heightLevel - minimum) * Rise;
        if (cell.UsesVertexCorners)
        {
            int mask = cell.CornerMask;
            float a = (mask & 1) != 0 ? 1 : 0, b = (mask & 2) != 0 ? 1 : 0;
            float c = (mask & 4) != 0 ? 1 : 0, d = (mask & 8) != 0 ? 1 : 0;
            return high + Rise * (v <= u ? a + (b - a) * u + (c - b) * v : a + (c - d) * u + (d - a) * v);
        }
        int direction = ramps.Length>0?ramps[index]:-1; if (direction < 0) return high;
        int2 low = new int2(index % Map.grid.width, index / Map.grid.width) - GridTerrain.RampDirections[direction];
        if (math.any(low < 0) || low.x >= Map.grid.width || low.y >= Map.grid.height) return high;
        float bottom = (Map.cells[low.y * Map.grid.width + low.x].heightLevel - minimum) * Rise;
        float t = direction == 0 ? 1 - v : direction == 1 ? u : direction == 2 ? v : 1 - u;
        return math.lerp(bottom, high, t);
    }
    public float4 Corners(int index) => new float4(CellHeight(index, 0, 0), CellHeight(index, 1, 0), CellHeight(index, 1, 1), CellHeight(index, 0, 1));
}
