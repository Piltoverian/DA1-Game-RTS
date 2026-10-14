using Unity.Collections;

/// <summary>Direction bits match IntegrationField's N,E,S,W,SE,NW,SW,NE ordering.</summary>
public static class GridNeighborMaskUtility
{
    public static byte Calculate(int index, int width, int height, NativeArray<GridNodeCost> costs)
    {
        if (costs[index].cost >= 255) return 0;
        int x = index % width;
        int y = index / width;
        byte mask = 0;
        for (int i = 0; i < 8; i++)
        {
            int dx = i == 1 || i == 4 || i == 7 ? 1 : i == 3 || i == 5 || i == 6 ? -1 : 0;
            int dy = i == 0 || i == 5 || i == 7 ? 1 : i == 2 || i == 4 || i == 6 ? -1 : 0;
            int nx = x + dx;
            int ny = y + dy;
            if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
            if (costs[ny * width + nx].cost >= 255) continue;
            if (i >= 4 && (costs[y * width + nx].cost >= 255 || costs[ny * width + x].cost >= 255)) continue;
            mask |= (byte)(1 << i);
        }
        return mask;
    }
}
