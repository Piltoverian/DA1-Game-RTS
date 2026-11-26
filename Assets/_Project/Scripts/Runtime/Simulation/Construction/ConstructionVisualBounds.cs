using Unity.Entities;
using Unity.Mathematics;

/// <summary>Mesh bounds in building-root local space, before grid fitting.</summary>
public struct ConstructionVisualBounds : IComponentData
{
    public float3 Min, Max;
}

public static class ConstructionRevealMath
{
    public static float Height(ConstructionVisualBounds bounds, float4x4 modelToWorld, float progress)
    {
        float bottom = float.MaxValue, top = float.MinValue;
        for (int i = 0; i < 8; i++)
        {
            float3 corner = math.select(bounds.Min, bounds.Max, new bool3((i & 1) != 0, (i & 2) != 0, (i & 4) != 0));
            float y = math.transform(modelToWorld, corner).y;
            bottom = math.min(bottom, y); top = math.max(top, y);
        }
        // Keep the last roof pixels and the unstarted foundation outside the clip plane.
        float margin = math.max(.05f, (top - bottom) * .01f);
        return math.lerp(bottom - margin, top + margin, math.saturate(progress));
    }
}
