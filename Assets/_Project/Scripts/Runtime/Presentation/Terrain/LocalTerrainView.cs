using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

// Local adapter: compile once per map, then select only this viewer's explored cells.
public sealed class LocalTerrainView : IDisposable
{
    public readonly TerrainView View = new();
    Entity sourceGrid;
    uint revision;
    TerrainCellVisualData[] compiled;
    TerrainSyncSettings settings;
    GridComponent previousGrid;
    int scope = -1, exploredVersion = -1;

    public bool Update(EntityManager em, LocalFogPresentationSystem fog)
    {
        using var grids = em.CreateEntityQuery(typeof(GridComponent), typeof(MapBuildState), typeof(TerrainSyncSettings), typeof(TerrainVisualSource));
        if (grids.CalculateEntityCount() != 1 || (fog != null && !fog.Frame.IsValid)) { Reset(); return false; }
        var entity = grids.GetSingletonEntity();
        var grid = em.GetComponentData<GridComponent>(entity);
        var state = em.GetComponentData<MapBuildState>(entity);
        if (state.Phase == MapBuildPhase.Requested || state.Phase == MapBuildPhase.Failed) { Reset(); return false; }
        var source = em.GetComponentObject<TerrainVisualSource>(entity);
        if (source.Revision != state.Revision || source.Cells == null || source.Cells.Length != checked(grid.width * grid.height))
        { Reset(); return false; }
        bool sourceChanged = sourceGrid != entity || revision != state.Revision
            || previousGrid.width != grid.width || previousGrid.height != grid.height || previousGrid.cellsize != grid.cellsize || math.any(previousGrid.origin != grid.origin)
            || !settings.Equals(source.Settings) || !ReferenceEquals(compiled, source.Cells);
        if (sourceChanged)
        {
            sourceGrid = entity; revision = state.Revision; previousGrid = grid;
            settings = source.Settings; compiled = source.Cells;
        }
        int currentScope = fog != null ? fog.ScopeVersion : 0;
        int currentExplored = fog != null ? fog.View.Terrain.Version : 0;
        if (sourceChanged || !View.Ready || scope != currentScope || exploredVersion != currentExplored)
        {
            var cells = new Dictionary<int, TerrainCellVisualData>();
            if (compiled != null)
            {
                if (fog == null) { for (int i = 0; i < compiled.Length; i++) cells.Add(i, compiled[i]); }
                else foreach (int i in fog.View.Terrain.Cells) if (i >= 0 && i < compiled.Length) cells.Add(i, compiled[i]);
            }
            View.SetGeometry(grid, settings, cells);
            scope = currentScope; exploredVersion = currentExplored;
        }
        if (fog != null) View.UpdateVisibility(fog.Frame.VisibleCells.AsArray());
        return true;
    }

    void Reset()
    {
        if (View.Ready) View.Dispose();
        compiled = null; sourceGrid = Entity.Null;
        scope = exploredVersion = -1;
    }

    public void Dispose() { Reset(); }
}
