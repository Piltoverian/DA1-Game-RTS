using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;

[BurstCompile]
public static class UnitMovementMath
{
    // --- 1. FLOW FIELD MATH ---

    /// <summary>Read the legal direction already compiled for the containing cell.</summary>
    public static float2 GetRawDirection(int2 cell, NativeArray<FieldNode> buffer, int gridWidth, int gridHeight)
    {
        if (!buffer.IsCreated || gridWidth <= 0 || gridHeight <= 0 || buffer.Length != gridWidth * gridHeight
            || cell.x < 0 || cell.y < 0 || cell.x >= gridWidth || cell.y >= gridHeight) return float2.zero;
        var node = buffer[cell.y * gridWidth + cell.x];
        return node.bestcost == int.MaxValue ? float2.zero : node.direction;
    }

    /// <summary>
    /// Discrete flow sampling: no bilinear averaging across obstacles or opposing paths.
    /// Turning remains smoothed by the actuator; navigation uses the containing XZ cell.
    /// </summary>
    public static float3 CalculateFlowVelocity(float3 pos, NativeArray<FieldNode> buffer, float3 gridOrigin, float cellSize, int gridWidth, int gridHeight, float speed)
    {
        if (!(cellSize > 0) || !math.all(math.isfinite(pos.xz)) || !math.isfinite(speed) || speed <= 0) return float3.zero;
        int2 cell = (int2)math.floor((pos.xz - gridOrigin.xz) / cellSize);
        float2 direction = GetRawDirection(cell, buffer, gridWidth, gridHeight);
        return new float3(direction.x, 0, direction.y) * speed;
    }
    public static float3 CalculateSafeFlowVelocity(float3 pos, NativeArray<FieldNode> nodes,
        NativeArray<GridNodeCost> costs, GridComponent grid, float radius, float speed)
    {
        var velocity = CalculateFlowVelocity(pos, nodes, grid.origin, grid.cellsize, grid.width, grid.height, speed);
        if (math.lengthsq(velocity) < 1e-8f) return velocity;
        int2 cell = GridHelper.WorldToGrid(pos, grid);
        int2 step = (int2)math.round(math.normalizesafe(velocity.xz));
        float2 centre = grid.origin.xz + ((float2)cell + .5f) * grid.cellsize;
        float2 nextCentre = centre + (float2)step * grid.cellsize;
        // A legal cell-to-cell direction may intersect a wall from an off-centre
        // position. Re-centre before turning; do not mix neighbouring flow vectors.
        float lookahead = math.min(speed * .25f, grid.cellsize * .75f);
        if (!GridObstacleAvoidance.ClearSegment(pos.xz, pos.xz + math.normalizesafe(velocity.xz) * lookahead, radius, costs, grid)) {
            float2 waypoint;
            if (GridObstacleAvoidance.ClearSegment(pos.xz, nextCentre, radius, costs, grid)) waypoint = nextCentre;
            else if (GridObstacleAvoidance.ClearSegment(pos.xz, centre, radius, costs, grid)) waypoint = centre;
            else return velocity;
            float2 direction = math.normalizesafe(waypoint - pos.xz);
            return new float3(direction.x, 0, direction.y) * speed;
        }
        return velocity;
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
        if(grid.cellsize<=0||searchRadius<=0||gridCosts.Length!=grid.width*grid.height||!math.all(math.isfinite(worldPos)))return float2.zero;
        int2 centralCell = GridHelper.WorldToGrid(worldPos, grid);
        float2 gradient = float2.zero;
        int searchSteps = (int)math.ceil(searchRadius / grid.cellsize);

        for (int x = -searchSteps; x <= searchSteps; x++)
        {
            for (int y = -searchSteps; y <= searchSteps; y++)
            {
                int2 neighbor = centralCell + new int2(x, y);
                if (neighbor.x < 0 || neighbor.x >= grid.width || neighbor.y < 0 || neighbor.y >= grid.height) continue;

                int idx = GridHelper.GetNodeIndex(neighbor, grid);
                if (gridCosts[idx].cost >= 255 || gridCosts[idx].cost == int.MaxValue) // Là vật cản
                {
                    // A blocked cell occupies an area, not just its centre.
                    // On coarse maps its centre can be outside the avoidance
                    // radius even while the unit is touching the obstacle.
                    float2 minimum=new float2(grid.origin.x+neighbor.x*grid.cellsize,grid.origin.z+neighbor.y*grid.cellsize);
                    float2 maximum=minimum+grid.cellsize;
                    float2 point=new float2(worldPos.x,worldPos.z);
                    float2 diff=point-math.clamp(point,minimum,maximum);
                    if(math.lengthsq(diff)<1e-10f){
                        // If already inside/on a blocked cell, escape through
                        // the nearest side instead of ignoring the current cell.
                        float4 distances=new float4(point.x-minimum.x,maximum.x-point.x,point.y-minimum.y,maximum.y-point.y);
                        int side=0;for(int s=1;s<4;s++)if(distances[s]<distances[side])side=s;
                        diff=(side==0?new float2(-1,0):side==1?new float2(1,0):side==2?new float2(0,-1):new float2(0,1))*.001f;
                    }
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

