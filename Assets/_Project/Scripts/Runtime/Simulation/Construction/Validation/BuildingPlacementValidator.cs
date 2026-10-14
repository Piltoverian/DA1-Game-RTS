using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public enum BuildingPlacementFailure
{
    None, MissingGridData, InvalidFootprint, InvalidSurface, MixedHeight, Blocked, UnitOccupied
}

// Read-only gameplay contract. Render height, bootstrap exit rings and RampInfluence
// are not placement rules. Call again in simulation after preview has accepted.
public static class BuildingPlacementValidator
{
    public static BuildingPlacementFailure Validate(in GridComponent grid, NativeArray<GridTerrain> terrain,
        NativeArray<GridNodeCost> costs, float3 rootPosition, StartEndRect localRect,
        NativeParallelMultiHashMap<int, Entity> unitBucket, out int2 min, out int2 max)
    {
        min = max = default;
        long count = (long)grid.width * grid.height;
        if (grid.width <= 0 || grid.height <= 0 || count > int.MaxValue || !math.isfinite(grid.cellsize)
            || grid.cellsize <= 0 || !math.all(math.isfinite(grid.origin)) || !terrain.IsCreated || !costs.IsCreated
            || terrain.Length != count || costs.Length != count)
            return BuildingPlacementFailure.MissingGridData;
        if (!math.all(math.isfinite(rootPosition)) || !math.all(math.isfinite(localRect.MinPoint))
            || !math.all(math.isfinite(localRect.MaxPoint)) || !math.all(localRect.MaxPoint > localRect.MinPoint)
            || !PlayerSpawnPlacement.TryFootprint(grid, rootPosition, localRect, out min, out max))
            return BuildingPlacementFailure.InvalidFootprint;

        long? level = null;
        for (int y = min.y; y <= max.y; y++)
        for (int x = min.x; x <= max.x; x++)
        {
            int index = GridHelper.GetNodeIndex(new int2(x, y), grid);
            var cell = terrain[index];
            if (!cell.walkable || cell.isMountain || cell.isCliff || cell.RampId >= 0
                || (cell.UsesVertexCorners && cell.CornerMask != 0 && cell.CornerMask != 15))
                return BuildingPlacementFailure.InvalidSurface;
            // TerrainSurfaceData adds one tier to every corner of mask15.
            long surfaceLevel = (long)cell.heightLevel + (cell.UsesVertexCorners && cell.CornerMask == 15 ? 1 : 0);
            if (level.HasValue && level.Value != surfaceLevel) return BuildingPlacementFailure.MixedHeight;
            level = surfaceLevel;
            if (costs[index].cost != 1) return BuildingPlacementFailure.Blocked;
            if (unitBucket.IsCreated && unitBucket.ContainsKey(index)) return BuildingPlacementFailure.UnitOccupied;
        }
        return BuildingPlacementFailure.None;
    }
}
