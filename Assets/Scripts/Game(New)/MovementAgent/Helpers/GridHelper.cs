using Unity.Entities;
using Unity.Mathematics;

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
}