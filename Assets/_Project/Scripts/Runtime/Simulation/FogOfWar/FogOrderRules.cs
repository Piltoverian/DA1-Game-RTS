using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

// Information captured at issue time; never refresh it from a hidden target.
public struct FogKnownTarget
{
    public bool HasValue;
    public FogRenderKind Kind;
    public float3 Position;
    public float2 Min, Max;
    public ResourceType ResourceType;
}

public struct FogTargetTravel : IComponentData
{
    public int PlayerId;
    public FogKnownTarget Known;
    public bool ApproachRequested;
}

public static class FogOrderRules
{
    public static bool TryGrid(EntityManager em, out Entity entity, out GridComponent grid)
    {
        using var q = em.CreateEntityQuery(typeof(GridComponent));
        entity = q.CalculateEntityCount() == 1 ? q.GetSingletonEntity() : Entity.Null;
        grid = entity != Entity.Null ? em.GetComponentData<GridComponent>(entity) : default;
        return entity != Entity.Null && SpatialCellCoverage.IsGridValid(grid);
    }

    public static bool HasFog(EntityManager em)
    {
        using var q = em.CreateEntityQuery(typeof(FogOfWarSpatialSystemData));
        return !q.IsEmptyIgnoreFilter;
    }

    public static bool CellsVisible(EntityManager em, int player, int2 min, int2 max, bool all)
    {
        using var q = em.CreateEntityQuery(typeof(FogOfWarSpatialSystemData));
        if (q.IsEmptyIgnoreFilter) return true; // Existing fixtures/maps without FOW.
        if (q.CalculateEntityCount() != 1 || !TryGrid(em, out _, out var grid)) return false;
        var fog = q.GetSingleton<FogOfWarSpatialSystemData>();
        for (int y = min.y; y <= max.y; y++)
        for (int x = min.x; x <= max.x; x++)
        {
            bool visible = PlayerSpawnPlacement.InBounds(new int2(x, y), grid) && fog.IsVisibleTo(y * grid.width + x, player);
            if (visible && !all) return true;
            if (!visible && all) return false;
        }
        return all;
    }

    public static bool IsVisible(EntityManager em, int player, Entity entity)
    {
        if (!em.Exists(entity) || !em.HasComponent<LocalTransform>(entity)) return false;
        if (!HasFog(em)) return true;
        if (!TryGrid(em, out _, out var grid)) return false;
        var pos = em.GetComponentData<LocalTransform>(entity).Position;
        int2 min = GridHelper.WorldToGrid(pos, grid), max = min;
        if (em.HasComponent<BlockageData>(entity) && !PlayerSpawnPlacement.TryFootprint(grid, pos,
            em.GetComponentData<BlockageData>(entity).LocalRect, out min, out max)) return false;
        return CellsVisible(em, player, min, max, false);
    }

    public static bool KnownAreaVisible(EntityManager em, int player, in FogKnownTarget known)
    {
        if (!TryGrid(em, out _, out var grid)) return !HasFog(em);
        var min = GridHelper.WorldToGrid(new float3(known.Min.x, 0, known.Min.y), grid);
        var max = GridHelper.WorldToGrid(new float3(known.Max.x - .001f, 0, known.Max.y - .001f), grid);
        return CellsVisible(em, player, min, max, false);
    }
}
