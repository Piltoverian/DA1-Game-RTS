using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;

[BurstCompile]
public static class UnitMovementMath
{
    // --- 1. FLOW FIELD MATH ---

    /// <summary>
    /// Lấy hướng thô tại một ô lưới dựa trên BestCost của hàng xóm.
    /// </summary>
    public static float2 GetRawDirection(int2 cell, NativeArray<FieldNode> buffer, int gridWidth, int gridHeight)
    {
        if (cell.x < 0 || cell.x >= gridWidth || cell.y < 0 || cell.y >= gridHeight) return float2.zero;

        int bestCost = buffer[cell.y * gridWidth + cell.x].bestcost;
        int2 bestDir = int2.zero;

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;
                int2 neighbor = cell + new int2(x, y);
                if (neighbor.x < 0 || neighbor.x >= gridWidth || neighbor.y < 0 || neighbor.y >= gridHeight) continue;

                int nCost = buffer[neighbor.y * gridWidth + neighbor.x].bestcost;
                if (nCost < bestCost)
                {
                    bestCost = nCost;
                    bestDir = new int2(x, y);
                }
            }
        }
        return math.normalizesafe(new float2(bestDir.x, bestDir.y));
    }

    /// <summary>
    /// Nội suy hướng di chuyển (Bilinear Interpolation) từ Flow Field.
    /// </summary>
    public static float3 CalculateFlowVelocity(float3 pos, NativeArray<FieldNode> buffer, float3 gridOrigin, float cellSize, int gridWidth, int gridHeight, float speed)
    {
        float2 gridFloatingPos = new float2(
            (pos.x - gridOrigin.x) / cellSize - 0.5f,
            (pos.z - gridOrigin.z) / cellSize - 0.5f
        );

        int2 cell00 = (int2)math.floor(gridFloatingPos);
        float2 t = gridFloatingPos - cell00;

        float2 d00 = GetRawDirection(cell00, buffer, gridWidth, gridHeight);
        float2 d10 = GetRawDirection(cell00 + new int2(1, 0), buffer, gridWidth, gridHeight);
        float2 d01 = GetRawDirection(cell00 + new int2(0, 1), buffer, gridWidth, gridHeight);
        float2 d11 = GetRawDirection(cell00 + new int2(1, 1), buffer, gridWidth, gridHeight);

        float2 interpolatedDir = math.lerp(
            math.lerp(d00, d10, t.x),
            math.lerp(d01, d11, t.x),
            t.y
        );

        if (math.lengthsq(interpolatedDir) > 0.001f)
            return new float3(interpolatedDir.x, 0, interpolatedDir.y) * speed;

        return float3.zero;
    }

    // --- 2. GRID GRADIENT MATH ---

    /// <summary>
    /// Tính toán vector Gradient hướng ra xa các vật cản gần nhất trên Grid.
    /// </summary>
    public static float2 CalculateGridGradient(
        float3 worldPos,
        NativeArray<GridNodeCost> gridCosts,
        GridComponent grid,
        float searchRadius)
    {
        int2 centralCell = GridHelper.WorldToGrid(worldPos, grid);
        float2 gradient = float2.zero;
        int searchSteps = (int)math.ceil(searchRadius / grid.cellsize);

        for (int x = -searchSteps; x <= searchSteps; x++)
        {
            for (int y = -searchSteps; y <= searchSteps; y++)
            {
                if (x == 0 && y == 0) continue;

                int2 neighbor = centralCell + new int2(x, y);
                if (neighbor.x < 0 || neighbor.x >= grid.width || neighbor.y < 0 || neighbor.y >= grid.height) continue;

                int idx = GridHelper.GetNodeIndex(neighbor, grid);
                if (gridCosts[idx].cost >= 255 || gridCosts[idx].cost == int.MaxValue) // Là vật cản
                {
                    float3 obstacleWorldPos = GridHelper.GridToWorld(neighbor, grid);
                    float2 diff = new float2(worldPos.x - obstacleWorldPos.x, worldPos.z - obstacleWorldPos.z);
                    float distSq = math.lengthsq(diff);

                    if (distSq < searchRadius * searchRadius)
                    {
                        // Lực đẩy tỉ lệ nghịch với bình phương khoảng cách
                        gradient += math.normalizesafe(diff) / math.max(0.1f, distSq);
                    }
                }
            }
        }
        return math.normalizesafe(gradient);
    }
}
