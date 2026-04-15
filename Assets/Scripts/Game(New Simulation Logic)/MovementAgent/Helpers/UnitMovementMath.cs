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

    // --- 2. AVOIDANCE MATH ---

    /// <summary>
    /// Tính toán các ngưỡng va chạm dựa trên tổng bán kính (Dynamic Sum-of-Radii).
    /// </summary>
    public static void CalculateSumOfRadii(float radiusA, float radiusB, out float sep, out float con, out float av)
    {
        float sum = radiusA + radiusB;
        sep = sum * 0.9f;
        con = sum + 0.5f;
        av = sum + 2.0f;
    }

    /// <summary>
    /// Tính nửa-góc bóng (apparent half-angle) mà một obstacle chiếm trên context map.
    /// combinedRadius = radiusA + radiusB (tổng bán kính hai vật thể)
    /// dist = khoảng cách giữa tâm hai vật thể
    /// Trả về: nửa góc bằng radian. Clamp [0, π/2] để tránh NaN khi chồng lấn.
    /// </summary>
    public static float CalculateApparentHalfAngle(float combinedRadius, float dist)
    {
        return math.asin(math.clamp(combinedRadius / math.max(dist, 0.01f), 0f, 1f));
    }

    /// <summary>
    /// Tính lực đẩy separation có xét radius (phi tuyến).
    /// dist = khoảng cách giữa tâm hai vật thể
    /// localSeparationZone = ngưỡng separation (từ CalculateSumOfRadii)
    /// combinedRadius = radiusA + radiusB
    /// </summary>
    public static float CalculateSeparationMagnitude(float dist, float localSeparationZone, float combinedRadius)
    {
        if (dist >= localSeparationZone) return 0f;

        float penetration = (localSeparationZone - dist) / localSeparationZone;
        // Tuyến tính: penetration^1.0 → overlap trung bình cũng đẩy đủ mạnh
        // (1.5 quá yếu ở moderate overlap: 0.44^1.5=0.29, không thắng goal velocity)
        float nonLinearPush = penetration;
        // Radius scale: object lớn cần lực đẩy lớn hơn tỉ lệ
        float radiusScale = combinedRadius * 0.5f;  // normalized: radius=1+1 → scale=1.0

        return nonLinearPush * math.max(radiusScale, 1.0f);
    }

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
                if (gridCosts[idx].cost >= 250) // Là vật cản
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

    /// <summary>
    /// Tính toán mức độ nguy hiểm (Danger) có xét đến vận tốc tương đối (Time-To-Collision lite)
    /// và angular coverage (Radius-Aware).
    /// angularCoverage: [0, 1] = tỉ lệ góc bóng / (π/2), cho biết object chiếm bao nhiêu % bán cầu.
    /// </summary>
    public static float CalculateDanger(
        float dist,
        float avoidRadius,
        float contactZone,
        float dot,
        float staticMultiplier,
        float3 myVel,
        float3 neighborVel,
        float angularCoverage)
    {
        // 1. Khoảng cách cơ bản (Distance-based danger)
        float distanceDanger = math.clamp((avoidRadius - dist) / (avoidRadius - contactZone), 0f, 1f);
        // angularCoverage boost: object chiếm nhiều góc → danger mạnh hơn trên mỗi slot bị ảnh hưởng
        // Base = 1.0 (small object), lên tới 2.0 (rất lớn, chiếm ~π/2 radian)
        float radiusBoost = 1.0f + angularCoverage;
        float danger = distanceDanger * dot * radiusBoost;

        // 2. Velocity-aware adjustment (ORCA-lite)
        // Nếu hai đơn vị đang đi cùng hướng (Consensus cao), chúng ta giảm nguy hiểm.
        float consensus = math.dot(math.normalizesafe(myVel), math.normalizesafe(neighborVel));

        if (consensus > 0.8f)
        {
            // Kiểm tra tốc độ tiếp cận: nếu tôi nhanh hơn → đang đuổi kịp, KHÔNG giảm danger
            float mySpeed = math.length(myVel);
            float neighborSpeed = math.length(neighborVel);
            float speedRatio = mySpeed / math.max(neighborSpeed, 0.1f);

            if (speedRatio > 1.2f)
            {
                // Đuổi kịp: scale danger theo tốc độ tiếp cận
                float catchUpFactor = math.clamp(speedRatio - 1.0f, 0f, 1f);
                danger *= math.lerp(0.1f, 1.0f, catchUpFactor);
            }
            else
            {
                // Thực sự song song đồng tốc: giảm danger bình thường
                danger *= 0.1f;
            }
        }
        else if (consensus < -0.5f)
        {
            // Đi đối đầu: Tăng danger lên để né sớm
            danger *= 1.5f;
        }

        // 3. Xử lý vật thể tĩnh
        danger *= staticMultiplier;

        return math.clamp(danger, 0f, 1.5f);
    }

    // --- 3. SOLVER MATH ---

    /// <summary>
    /// Tính toán độ lệch (offset) dựa trên nội suy Parabol để hướng đi mượt hơn.
    /// </summary>
    public static float CalculateQuadraticOffset(float vM, float vC, float vP)
    {
        float denominator = vM - 2 * vC + vP;
        return (math.abs(denominator) > 0.001f) ? 0.5f * (vM - vP) / denominator : 0f;
    }
}
