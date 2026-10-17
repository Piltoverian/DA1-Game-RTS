using System.Collections.Generic;
using Unity.Mathematics;

public struct FogTerrainCellData
{
    public int CellIndex;
    // Static terrain only. Dynamic occupancy/navigation costs are not sent.
    public GridTerrain Terrain;
}

// Receiver-owned explored terrain. Collect sends the full authorized set every time.
// The session owner must Clear at match boundaries, even for identical map geometry.
public sealed class FogTerrainMemory
{
    private readonly Dictionary<int, GridTerrain> cells = new();
    private bool hasFrame;
    private int playerId, width, height;
    private float cellSize;
    private float3 origin;

    public IReadOnlyDictionary<int, GridTerrain> Cells => cells;

    private bool Matches(FogOfWarFrame frame) => hasFrame && playerId == frame.PlayerId
        && width == frame.Width && height == frame.Height && cellSize == frame.CellSize
        && math.all(origin == frame.Origin);

    public void Clear() { cells.Clear(); hasFrame = false; }

    public void Apply(FogOfWarFrame frame)
    {
        if (!frame.IsValid) { Clear(); return; }
        if (!Matches(frame)) Clear();
        playerId = frame.PlayerId; width = frame.Width; height = frame.Height;
        cellSize = frame.CellSize; origin = frame.Origin; hasFrame = true;
        foreach (var cell in frame.TerrainCells) cells[cell.CellIndex] = cell.Terrain;
    }
}
