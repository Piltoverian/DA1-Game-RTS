using Unity.Collections;

public static class IntegrationSeedUtility
{
    // Exactly one current island + walkable target => squared distance 0 is the
    // unique minimum. All other cases retain the original whole-grid search.
    public static bool TrySingleIslandTarget(int x, int y, int width, int height,
        int islandCount, uint islandTopologyVersion, uint topologyVersion,
        NativeArray<GridNodeCost> costs, NativeArray<GridIsland> islands,
        out int index, out int island)
    {
        index = -1;
        island = 0;
        if (islandCount != 1 || islandTopologyVersion != topologyVersion
            || width <= 0 || height <= 0 || x < 0 || x >= width || y < 0 || y >= height
            || costs.Length != width * height || islands.Length != costs.Length)
            return false;
        int candidate = y * width + x;
        int id = islands[candidate].islandID;
        if (costs[candidate].cost >= 255 || id <= 0 || id >= 1000) return false;
        index = candidate;
        island = id;
        return true;
    }
}
