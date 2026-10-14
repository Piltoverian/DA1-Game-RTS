using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

// Swept disc against blocked grid squares, including rounded corners and map edges.
// Allocation-free: the broad phase visits only cells overlapped by the swept footprint.
[BurstCompile]
public static class GridObstacleAvoidance
{
    static bool Valid(NativeArray<GridNodeCost> costs, GridComponent grid)
        => costs.IsCreated && grid.cellsize > 0 && grid.width > 0 && grid.height > 0
            && costs.Length == grid.width * grid.height;

    static void Hit(float candidate, float2 candidateNormal, ref float time, ref float2 normal)
    {
        if (candidate < -1e-6f || candidate > 1) return;
        candidate = math.max(0, candidate);
        if (candidate < time - 1e-5f) { time = candidate; normal = candidateNormal; }
        else if (math.abs(candidate - time) <= 1e-5f) normal = math.normalizesafe(normal + candidateNormal);
    }

    static void Face(float2 position, float2 delta, float plane, float minimum, float maximum,
        bool xAxis, float2 faceNormal, ref float time, ref float2 normal)
    {
        if (math.dot(delta, faceNormal) >= -1e-8f) return;
        float component = xAxis ? delta.x : delta.y;
        float candidate = (plane - (xAxis ? position.x : position.y)) / component;
        float other = xAxis ? position.y + candidate * delta.y : position.x + candidate * delta.x;
        if (other >= minimum - 1e-6f && other <= maximum + 1e-6f)
            Hit(candidate, faceNormal, ref time, ref normal);
    }

    static void Corner(float2 position, float2 delta, float2 corner, float2 quadrant, float radius,
        ref float time, ref float2 normal)
    {
        float2 offset = position - corner;
        float a = math.lengthsq(delta), b = math.dot(offset, delta);
        float discriminant = b * b - a * (math.lengthsq(offset) - radius * radius);
        if (discriminant < 0 || a <= 1e-12f) return;
        float candidate = (-b - math.sqrt(discriminant)) / a;
        if (candidate < -1e-6f || candidate > time + 1e-5f) return;
        float2 contact = offset + delta * candidate;
        if (math.any(contact * quadrant < -1e-6f)) return;
        float2 n = math.normalizesafe(contact);
        if (math.dot(delta, n) < -1e-8f) Hit(candidate, n, ref time, ref normal);
    }

    public static bool Sweep(float2 position, float2 delta, float radius, NativeArray<GridNodeCost> costs,
        GridComponent grid, out float time, out float2 normal)
    {
        time = 1; normal = float2.zero;
        if (!Valid(costs, grid) || math.lengthsq(delta) < 1e-12f) return false;
        float r = math.max(0, radius) + grid.cellsize * .001f;
        float2 gridMin = grid.origin.xz, gridMax = gridMin + new float2(grid.width, grid.height) * grid.cellsize;
        // Treat the outside of the map as solid, rather than allowing an out-of-bounds step.
        Face(position, delta, gridMin.x + r, float.MinValue, float.MaxValue, true, new float2(1, 0), ref time, ref normal);
        Face(position, delta, gridMax.x - r, float.MinValue, float.MaxValue, true, new float2(-1, 0), ref time, ref normal);
        Face(position, delta, gridMin.y + r, float.MinValue, float.MaxValue, false, new float2(0, 1), ref time, ref normal);
        Face(position, delta, gridMax.y - r, float.MinValue, float.MaxValue, false, new float2(0, -1), ref time, ref normal);
        int2 first = math.max((int2)math.floor((math.min(position, position + delta) - r - gridMin) / grid.cellsize), 0);
        int2 last = math.min((int2)math.floor((math.max(position, position + delta) + r - gridMin) / grid.cellsize),
            new int2(grid.width - 1, grid.height - 1));
        for (int z = first.y; z <= last.y; z++) for (int x = first.x; x <= last.x; x++) {
            if (costs[z * grid.width + x].cost < 255) continue;
            float2 minimum = gridMin + new float2(x, z) * grid.cellsize, maximum = minimum + grid.cellsize;
            float2 away = position - math.clamp(position, minimum, maximum);
            float distanceSq = math.lengthsq(away);
            if (distanceSq < r * r - 1e-7f) {
                // Existing overlap (e.g. a newly placed obstacle): permit escape, block worsening it.
                float2 escape = math.normalizesafe(away);
                if (distanceSq < 1e-10f) {
                    float4 sides = new float4(position.x - minimum.x, maximum.x - position.x,
                        position.y - minimum.y, maximum.y - position.y);
                    int side = 0; for (int s = 1; s < 4; s++) if (sides[s] < sides[side]) side = s;
                    escape = side == 0 ? new float2(-1, 0) : side == 1 ? new float2(1, 0)
                        : side == 2 ? new float2(0, -1) : new float2(0, 1);
                }
                if (math.dot(delta, escape) < -1e-8f) Hit(0, escape, ref time, ref normal);
                continue;
            }
            Face(position, delta, minimum.x - r, minimum.y, maximum.y, true, new float2(-1, 0), ref time, ref normal);
            Face(position, delta, maximum.x + r, minimum.y, maximum.y, true, new float2(1, 0), ref time, ref normal);
            Face(position, delta, minimum.y - r, minimum.x, maximum.x, false, new float2(0, -1), ref time, ref normal);
            Face(position, delta, maximum.y + r, minimum.x, maximum.x, false, new float2(0, 1), ref time, ref normal);
            Corner(position, delta, minimum, new float2(-1, -1), r, ref time, ref normal);
            Corner(position, delta, new float2(maximum.x, minimum.y), new float2(1, -1), r, ref time, ref normal);
            Corner(position, delta, maximum, new float2(1, 1), r, ref time, ref normal);
            Corner(position, delta, new float2(minimum.x, maximum.y), new float2(-1, 1), r, ref time, ref normal);
        }
        return math.lengthsq(normal) > 0;
    }

    public static float2 Steer(float2 position, float2 velocity, float radius, NativeArray<GridNodeCost> costs,
        GridComponent grid, float horizon = .25f)
    {
        float length = math.length(velocity);
        if (length < 1e-5f || !Valid(costs, grid)) return velocity;
        float distance = math.min(length * horizon, grid.cellsize * .75f);
        // Anticipation comes from the lookahead segment, not an inflated radius that closes narrow passages.
        if (!Sweep(position, velocity * (distance / length), radius, costs, grid, out float time, out var normal))
            return velocity;
        float weight = 1 - time * time * (3 - 2 * time);
        return velocity - normal * (math.min(0, math.dot(velocity, normal)) * weight);
    }

    public static float2 MoveAndSlide(float2 position, float2 delta, float radius, NativeArray<GridNodeCost> costs, GridComponent grid)
    {
        if (!Valid(costs, grid)) return position + delta;
        float2 initial = position;
        for (int iteration = 0; iteration < 3 && math.lengthsq(delta) > 1e-10f; iteration++) {
            if (!Sweep(position, delta, radius, costs, grid, out float time, out var normal)) {
                position += delta;
                break;
            }
            float travel = math.max(0, time - 1e-4f);
            position += delta * travel;
            delta *= 1 - travel;
            delta -= normal * math.min(0, math.dot(delta, normal));
        }
        // Numerical contact errors and combined contact normals must never turn
        // a clear starting footprint into penetration. Existing overlaps can escape.
        return FootprintClear(initial, radius, costs, grid) && !FootprintClear(position, radius, costs, grid)
            ? initial : position;
    }

    public static bool FootprintClear(float2 position, float radius, NativeArray<GridNodeCost> costs, GridComponent grid)
    {
        if (!Valid(costs, grid)) return true;
        if (math.any(position - radius < grid.origin.xz) || math.any(position + radius > grid.origin.xz + new float2(grid.width, grid.height) * grid.cellsize)) return false;
        int2 first = math.max((int2)math.floor((position - radius - grid.origin.xz) / grid.cellsize), 0);
        int2 last = math.min((int2)math.floor((position + radius - grid.origin.xz) / grid.cellsize), new int2(grid.width - 1, grid.height - 1));
        for (int z = first.y; z <= last.y; z++) for (int x = first.x; x <= last.x; x++) {
            if (costs[z * grid.width + x].cost < 255) continue;
            float2 minimum = grid.origin.xz + new float2(x, z) * grid.cellsize;
            if (math.distancesq(position, math.clamp(position, minimum, minimum + grid.cellsize)) < radius * radius - 1e-6f) return false;
        }
        return true;
    }

    public static bool ClearSegment(float2 start, float2 end, float radius, NativeArray<GridNodeCost> costs, GridComponent grid)
        => !Sweep(start, end - start, radius, costs, grid, out _, out _);
}
