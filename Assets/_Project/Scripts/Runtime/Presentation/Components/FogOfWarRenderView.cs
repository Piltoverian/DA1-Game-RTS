using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Entities;

// Presentation memory only. It has no EntityManager and cannot inspect hidden simulation state.
// The session owner must Clear when leaving or starting a match.
public sealed class FogOfWarRenderView
{
    private readonly Dictionary<Entity, FogEntityRenderData> entities = new();
    private readonly HashSet<Entity> visibleIds = new();
    private readonly HashSet<int> visibleCells = new();
    private readonly List<Entity> previousIds = new();
    private bool hasFrame;
    private int playerId, width, height;
    private float cellSize;
    private float3 origin;

    public IReadOnlyDictionary<Entity, FogEntityRenderData> Entities => entities;
    public FogTerrainMemory Terrain { get; } = new();

    public void Clear()
    {
        entities.Clear(); visibleIds.Clear(); visibleCells.Clear(); previousIds.Clear();
        Terrain.Clear();
        hasFrame = false;
    }

    public void Apply(FogOfWarFrame frame)
    {
        if (!frame.IsValid) { Clear(); return; }
        if (!hasFrame || playerId != frame.PlayerId || width != frame.Width || height != frame.Height
            || cellSize != frame.CellSize || math.any(origin != frame.Origin)) Clear();
        hasFrame = true;
        playerId = frame.PlayerId;
        width = frame.Width; height = frame.Height; cellSize = frame.CellSize; origin = frame.Origin;
        Terrain.Apply(frame);
        visibleIds.Clear(); visibleCells.Clear(); previousIds.Clear();
        foreach (var cell in frame.VisibleCells) visibleCells.Add(cell);
        foreach (var incoming in frame.Entities)
        {
            var data = incoming;
            data.IsVisible = true;
            visibleIds.Add(data.Entity);
            entities[data.Entity] = data;
        }
        previousIds.AddRange(entities.Keys);
        foreach (var id in previousIds)
        {
            if (visibleIds.Contains(id)) continue;
            var remembered = entities[id];
            if (!remembered.IsStatic || IsFootprintVisible(remembered)) entities.Remove(id);
            else
            {
                remembered.IsVisible = false;
                entities[id] = remembered;
            }
        }
    }

    private bool IsFootprintVisible(in FogEntityRenderData data)
    {
        for (int z = data.FootprintMin.y; z <= data.FootprintMax.y; z++)
        for (int x = data.FootprintMin.x; x <= data.FootprintMax.x; x++)
            if (visibleCells.Contains(z * width + x)) return true;
        return false;
    }
}
