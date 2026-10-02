using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public static class GridHelper
{
    public static int2 WorldToGrid(float3 worldPos, GridComponent grid)
    {
        float xLocal = (worldPos.x - grid.origin.x) / grid.cellsize;
        float yLocal = (worldPos.z - grid.origin.z) / grid.cellsize;
        return new int2((int)math.floor(xLocal), (int)math.floor(yLocal));
    }

    public static float3 GridToWorld(int2 gridPos, GridComponent grid)
    {
        float x = grid.origin.x + gridPos.x * grid.cellsize + grid.cellsize / 2;
        float z = grid.origin.z + gridPos.y * grid.cellsize + grid.cellsize / 2;
        return new float3(x, 0, z);
    }

    public static int GetNodeIndex(int2 gridPos, GridComponent grid)
    {
        return gridPos.y * grid.width + gridPos.x;
    }

    public static int2 GetGridPosFromIndex(int index, GridComponent grid)
    {
        int x = index % grid.width;
        int y = index / grid.width;
        return new int2(x, y);
    }

    public static GridRect ConvertWorldRectToGridRect(GridRect gridRect,GridComponent grid)
    {
        int2 minGrid= WorldToGrid(new float3(gridRect.min.x, 0, gridRect.min.y), grid);
        int2 maxGrid= WorldToGrid(new float3(gridRect.max.x, 0, gridRect.max.y), grid);
        return new GridRect(minGrid, maxGrid);
    }
}

public struct GridRect
    {
    public int2 min;
    public int2 max;
    public GridRect(int2 min, int2 max)
    {
        this.min = min;
        this.max = max;
    }
}

public static class NaturalBlockedTargetResolver
{
    public const float TieBreakTolerance = 0.0001f;

    public static bool IsCellInBounds(int2 cell, in GridComponent grid)
    {
        return cell.x >= 0 && cell.x < grid.width && cell.y >= 0 && cell.y < grid.height;
    }

    public static bool IsCellWalkable(int2 cell, in GridComponent grid, in NativeArray<GridNodeCost> gridCosts)
    {
        int index = GridHelper.GetNodeIndex(cell, grid);
        int cost = gridCosts[index].cost;
        return cost < 255 && cost != int.MaxValue;
    }

    public static bool TryGetBlockageGridBounds(
        int2 targetCell,
        in GridComponent grid,
        in NativeArray<BlockageData> blockageDatas,
        in NativeArray<LocalToWorld> blockageLocalToWorlds,
        out int2 bMinGrid,
        out int2 bMaxGrid)
    {
        bMinGrid = targetCell;
        bMaxGrid = targetCell;

        for (int b = 0; b < blockageDatas.Length; b++)
        {
            BlockageData bData = blockageDatas[b];
            float3 bPos = blockageLocalToWorlds[b].Position;

            float2 worldMin = new float2(bPos.x + bData.LocalRect.MinPoint.x, bPos.z + bData.LocalRect.MinPoint.y);
            float2 worldMax = new float2(bPos.x + bData.LocalRect.MaxPoint.x, bPos.z + bData.LocalRect.MaxPoint.y);

            StartEndRect worldRect = new StartEndRect(worldMin);
            worldRect.ExpandTo(worldMax);

            float3 minWorld3D = new float3(worldRect.MinPoint.x + 0.01f, 0, worldRect.MinPoint.y + 0.01f);
            float3 maxWorld3D = new float3(worldRect.MaxPoint.x - 0.01f, 0, worldRect.MaxPoint.y - 0.01f);

            int2 bMin = GridHelper.WorldToGrid(minWorld3D, grid);
            int2 bMax = GridHelper.WorldToGrid(maxWorld3D, grid);

            int2 minGrid = math.min(bMin, bMax);
            int2 maxGrid = math.max(bMin, bMax);

            if (targetCell.x >= minGrid.x && targetCell.x <= maxGrid.x &&
                targetCell.y >= minGrid.y && targetCell.y <= maxGrid.y)
            {
                bMinGrid = minGrid;
                bMaxGrid = maxGrid;
                return true;
            }
        }

        return false;
    }

    public static bool TryResolveTarget(
        float3 rawWorldTarget,
        in GridComponent grid,
        in NativeArray<GridNodeCost> gridCosts,
        in NativeArray<BlockageData> blockageDatas,
        in NativeArray<LocalToWorld> blockageLocalToWorlds,
        out int2 resolvedCell,
        out float3 resolvedWorldTarget,
        out TargetResolutionKind resolutionKind)
    {
        int2 rawTargetCell = GridHelper.WorldToGrid(rawWorldTarget, grid);
        if (!IsCellInBounds(rawTargetCell, grid))
        {
            resolvedCell = rawTargetCell;
            resolvedWorldTarget = rawWorldTarget;
            resolutionKind = TargetResolutionKind.None;
            return false;
        }

        if (TryGetBlockageGridBounds(rawTargetCell, grid, blockageDatas, blockageLocalToWorlds, out _, out _))
        {
            resolvedCell = rawTargetCell;
            resolvedWorldTarget = rawWorldTarget;
            resolutionKind = TargetResolutionKind.BuildingBlockage;
            return true;
        }

        if (IsCellWalkable(rawTargetCell, grid, gridCosts))
        {
            resolvedCell = rawTargetCell;
            resolvedWorldTarget = rawWorldTarget;
            resolutionKind = TargetResolutionKind.Direct;
            return true;
        }

        if (TryResolveNaturalBlockedCell(
                rawWorldTarget,
                rawTargetCell,
                grid,
                gridCosts,
                out resolvedCell,
                out resolvedWorldTarget))
        {
            resolutionKind = TargetResolutionKind.NaturalResolved;
            return true;
        }

        resolutionKind = TargetResolutionKind.None;
        return false;
    }

    public static bool TryResolveNaturalBlockedCell(
        float3 rawWorldTarget,
        int2 startCell,
        in GridComponent grid,
        in NativeArray<GridNodeCost> gridCosts,
        out int2 resolvedCell,
        out float3 resolvedWorldTarget)
    {
        float2 clickXZ = new float2(rawWorldTarget.x, rawWorldTarget.z);
        bool foundAny = false;
        float bestDistance = float.MaxValue;
        int2 bestCell = startCell;

        for (int y = startCell.y + 1; y < grid.height; y++)
        {
            int2 candidate = new int2(startCell.x, y);
            if (IsCellWalkable(candidate, grid, gridCosts))
            {
                float2 entryPoint = new float2(clickXZ.x, grid.origin.z + candidate.y * grid.cellsize);
                float distance = math.distance(clickXZ, entryPoint);
                if (!foundAny || distance < bestDistance - TieBreakTolerance)
                {
                    bestDistance = distance;
                    bestCell = candidate;
                    foundAny = true;
                }
                break;
            }
        }

        for (int x = startCell.x + 1; x < grid.width; x++)
        {
            int2 candidate = new int2(x, startCell.y);
            if (IsCellWalkable(candidate, grid, gridCosts))
            {
                float2 entryPoint = new float2(grid.origin.x + candidate.x * grid.cellsize, clickXZ.y);
                float distance = math.distance(clickXZ, entryPoint);
                if (!foundAny || distance < bestDistance - TieBreakTolerance)
                {
                    bestDistance = distance;
                    bestCell = candidate;
                    foundAny = true;
                }
                break;
            }
        }

        for (int y = startCell.y - 1; y >= 0; y--)
        {
            int2 candidate = new int2(startCell.x, y);
            if (IsCellWalkable(candidate, grid, gridCosts))
            {
                float2 entryPoint = new float2(clickXZ.x, grid.origin.z + (candidate.y + 1) * grid.cellsize);
                float distance = math.distance(clickXZ, entryPoint);
                if (!foundAny || distance < bestDistance - TieBreakTolerance)
                {
                    bestDistance = distance;
                    bestCell = candidate;
                    foundAny = true;
                }
                break;
            }
        }

        for (int x = startCell.x - 1; x >= 0; x--)
        {
            int2 candidate = new int2(x, startCell.y);
            if (IsCellWalkable(candidate, grid, gridCosts))
            {
                float2 entryPoint = new float2(grid.origin.x + (candidate.x + 1) * grid.cellsize, clickXZ.y);
                float distance = math.distance(clickXZ, entryPoint);
                if (!foundAny || distance < bestDistance - TieBreakTolerance)
                {
                    bestDistance = distance;
                    bestCell = candidate;
                    foundAny = true;
                }
                break;
            }
        }

        if (foundAny)
        {
            resolvedCell = bestCell;
            resolvedWorldTarget = GridHelper.GridToWorld(bestCell, grid);
            return true;
        }

        resolvedCell = startCell;
        resolvedWorldTarget = rawWorldTarget;
        return false;
    }
}

