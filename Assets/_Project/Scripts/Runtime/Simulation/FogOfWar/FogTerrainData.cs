using System.Collections.Generic;
using Unity.Mathematics;

// Explored membership only. Static geometry lives in TerrainVisualSource/TerrainView.
public sealed class FogTerrainMemory
{
    readonly HashSet<int> cells = new();
    readonly HashSet<int> incoming = new();
    bool hasFrame;
    int playerId, width, height;
    float cellSize;
    float3 origin;
    public IReadOnlyCollection<int> Cells => cells;
    public int Version { get; private set; }
    public void Clear() { cells.Clear(); incoming.Clear(); hasFrame = false; Version++; }
    public void Apply(FogOfWarFrame frame)
    {
        if (!frame.IsValid) { if (hasFrame) Clear(); return; }
        bool scopeChanged = !hasFrame || playerId != frame.PlayerId || width != frame.Width || height != frame.Height
            || cellSize != frame.CellSize || math.any(origin != frame.Origin);
        incoming.Clear();
        foreach (int cell in frame.ExploredCells) incoming.Add(cell);
        if (scopeChanged || !cells.SetEquals(incoming))
        {
            cells.Clear(); cells.UnionWith(incoming); Version++;
        }
        playerId = frame.PlayerId; width = frame.Width; height = frame.Height;
        cellSize = frame.CellSize; origin = frame.Origin; hasFrame = true;
    }
}
