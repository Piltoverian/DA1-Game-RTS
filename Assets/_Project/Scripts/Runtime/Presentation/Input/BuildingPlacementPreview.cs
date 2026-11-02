using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public enum BuildingPreviewState { Unknown, Valid, Invalid }

public static class BuildingPlacementPreview
{
    public static BuildingPreviewState Evaluate(EntityManager em, int player, float3 position, StartEndRect rect)
    {
        if (!FogOrderRules.TryGrid(em, out var gridEntity, out var grid)
            || !PlayerSpawnPlacement.TryFootprint(grid, position, rect, out var min, out var max)
            || !em.HasBuffer<GridTerrain>(gridEntity)) return BuildingPreviewState.Invalid;
        using var fogQuery = em.CreateEntityQuery(typeof(FogOfWarSpatialSystemData));
        bool hasFog = !fogQuery.IsEmptyIgnoreFilter;
        var fog = fogQuery.CalculateEntityCount() == 1 ? fogQuery.GetSingleton<FogOfWarSpatialSystemData>() : default;
        var terrain = em.GetBuffer<GridTerrain>(gridEntity, true);
        if (terrain.Length != grid.width * grid.height) return BuildingPreviewState.Invalid;
        bool allVisible = true;
        long? level = null;
        for (int y = min.y; y <= max.y; y++)
        for (int x = min.x; x <= max.x; x++)
        {
            int index = y * grid.width + x;
            bool visible = !hasFog || fog.IsVisibleTo(index, player);
            allVisible &= visible;
            if (hasFog && !visible && !fog.IsExploredBy(index, player)) continue;
            var cell = terrain[index];
            if (!cell.walkable || cell.isMountain || cell.isCliff || cell.RampId >= 0
                || (cell.UsesVertexCorners && cell.CornerMask != 0 && cell.CornerMask != 15)) return BuildingPreviewState.Invalid;
            long height = (long)cell.heightLevel + (cell.UsesVertexCorners && cell.CornerMask == 15 ? 1 : 0);
            if (level.HasValue && level.Value != height) return BuildingPreviewState.Invalid;
            level = height;
        }
        if (!allVisible) return BuildingPreviewState.Unknown;
        if (!em.HasBuffer<GridNodeCost>(gridEntity)) return BuildingPreviewState.Invalid;
        using var buckets = em.CreateEntityQuery(typeof(MovementAgentBucket));
        var bucket = buckets.CalculateEntityCount() == 1 ? buckets.GetSingleton<MovementAgentBucket>().Bucket : default;
        return BuildingPlacementValidator.Validate(grid, terrain.AsNativeArray(), em.GetBuffer<GridNodeCost>(gridEntity, true).AsNativeArray(),
            position, rect, bucket, out _, out _) == BuildingPlacementFailure.None ? BuildingPreviewState.Valid : BuildingPreviewState.Invalid;
    }

}
