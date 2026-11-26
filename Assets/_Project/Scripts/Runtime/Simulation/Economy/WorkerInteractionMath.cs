using Unity.Mathematics;

public static class WorkerInteractionMath
{
    // Navigation finishes at a free perimeter cell, including diagonal corners,
    // and then stops within steering distance of that cell's center.
    public static float ReachDistance(float configuredDistanceSq, float cellSize, float stoppingDistance) =>
        math.max(math.sqrt(math.max(0, configuredDistanceSq)),
            math.max(0, cellSize) * .7071068f + math.max(0, stoppingDistance) + .075f);
}
