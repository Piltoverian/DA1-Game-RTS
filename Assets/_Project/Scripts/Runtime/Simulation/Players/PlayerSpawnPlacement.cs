using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public static class PlayerSpawnPlacement
{
    public static bool InBounds(int2 cell, GridComponent grid) =>
        cell.x >= 0 && cell.y >= 0 && cell.x < grid.width && cell.y < grid.height;

    // BlockageData already contains the baked world-space scale. Match CostChangeSystem's inset.
    public static bool TryFootprint(GridComponent grid, float3 position, StartEndRect rect, out int2 min, out int2 max)
    {
        float inset = math.min(.01f, math.cmin(rect.MaxPoint - rect.MinPoint) * .25f);
        min = GridHelper.WorldToGrid(position + new float3(rect.MinPoint.x + inset, 0, rect.MinPoint.y + inset), grid);
        max = GridHelper.WorldToGrid(position + new float3(rect.MaxPoint.x - inset, 0, rect.MaxPoint.y - inset), grid);
        return math.all(math.isfinite(position)) && math.all(math.isfinite(rect.MinPoint)) &&
            math.all(math.isfinite(rect.MaxPoint)) && math.all(rect.MaxPoint > rect.MinPoint) &&
            InBounds(min, grid) && InBounds(max, grid) && math.all(min <= max);
    }

    public static bool TryBuilding(GridComponent grid, NativeArray<GridTerrain> terrain, NativeArray<GridNodeCost> costs, NativeArray<bool> occupied,
        int2 anchor, int radius, StartEndRect rect, out float3 position, out int2 min, out int2 max, int cells = 0, int minDistance = 0)
    {
        position = default; min = default; max = default;
        if (!InBounds(anchor, grid)) return false;
        int level = terrain[GridHelper.GetNodeIndex(anchor, grid)].heightLevel;
        for (int ring = 0; ring <= radius; ring++)
        for (int y = -ring; y <= ring; y++)
        for (int x = -ring; x <= ring; x++)
        {
            if (math.max(math.abs(x), math.abs(y)) != ring) continue;
            if (math.lengthsq(new float2(x, y)) < minDistance * minDistance) continue;
            int2 candidate = anchor + new int2(x, y);
            float3 world = GridHelper.GridToWorld(candidate, grid);
            if (cells > 0) world = GridSnapperMath.Snap(world, grid, cells);
            if (!TryFootprint(grid, world, rect, out var lo, out var hi)) continue;
            bool valid = true;
            // A free ring around the footprint supplies circulation and an exit.
            for (int cy = lo.y - 1; cy <= hi.y + 1 && valid; cy++)
            for (int cx = lo.x - 1; cx <= hi.x + 1; cx++)
            {
                int2 cell = new int2(cx, cy);
                if (!InBounds(cell, grid) || math.lengthsq((float2)(cell - anchor)) > radius * radius) { valid = false; break; }
                int index = GridHelper.GetNodeIndex(cell, grid);
                if (occupied[index] && cx >= lo.x && cx <= hi.x && cy >= lo.y && cy <= hi.y)
                { valid = false; break; }
                var t = terrain[index];
                if (costs[index].cost != 1 || !t.walkable || t.isMountain || t.isCliff || t.RampId >= 0 || t.heightLevel != level)
                { valid = false; break; }
            }
            if (!valid) continue;
            position = world; min = lo; max = hi;
            return true;
        }
        return false;
    }

    public static void Reserve(GridComponent grid, NativeArray<GridNodeCost> costs, int2 min, int2 max, int cost)
    {
        for (int y = min.y; y <= max.y; y++)
        for (int x = min.x; x <= max.x; x++)
            costs[GridHelper.GetNodeIndex(new int2(x, y), grid)] = new GridNodeCost { cost = cost };
    }

    public static bool TryUnit(GridComponent grid, NativeArray<GridNodeCost> costs, NativeArray<bool> occupied,
        int2 anchor, int radius, int2 footprintMin, int2 footprintMax, out int2 result)
    {
        result = default;
        // Flood from the house's free perimeter; all selected cells remain connected to its exit.
        using var visitedStorage = new NativeArray<bool>(costs.Length, Allocator.Temp);
        var visited = visitedStorage;
        using var queue = new NativeQueue<int2>(Allocator.Temp);
        int2 entry = footprintMin - new int2(1, 1);
        if (!InBounds(entry, grid)) return false;
        queue.Enqueue(entry);
        visited[GridHelper.GetNodeIndex(entry, grid)] = true;
        while (queue.TryDequeue(out var cell))
        {
            int index = GridHelper.GetNodeIndex(cell, grid);
            if (costs[index].cost != 1 || math.lengthsq((float2)(cell - anchor)) > radius * radius) continue;
            if (!occupied[index]) { result = cell; return true; }
            for (int direction = 0; direction < 4; direction++)
            {
                int2 next = cell + GridTerrain.RampDirections[direction];
                if (!InBounds(next, grid)) continue;
                int ni = GridHelper.GetNodeIndex(next, grid);
                if (visited[ni]) continue;
                visited[ni] = true;
                queue.Enqueue(next);
            }
        }
        return false;
    }
}
