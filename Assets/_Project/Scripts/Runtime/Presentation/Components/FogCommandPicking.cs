using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public static class FogCommandPicking
{
    public static LocalFogPresentationSystem Local(World world, int player)
    {
        var fog = world?.GetExistingSystemManaged<LocalFogPresentationSystem>();
        return fog != null && fog.Frame.IsValid && fog.Frame.PlayerId == player ? fog : null;
    }

    public static bool TryPick(LocalFogPresentationSystem fog, Ray ray, FogRenderKind kind,
        out FogEntityRenderData picked, out FogKnownTarget known)
    {
        picked = default; known = default; float best = float.MaxValue;
        foreach (var pair in fog.View.Entities)
        {
            var data = pair.Value;
            if (data.Kind != FogRenderKind.Building && data.Kind != FogRenderKind.Resource) continue;
            var frame = fog.Frame;
            float2 min = frame.Origin.xz + (float2)data.FootprintMin * frame.CellSize;
            float2 max = frame.Origin.xz + ((float2)data.FootprintMax + 1) * frame.CellSize;
            var bounds = new Bounds(new Vector3((min.x + max.x) * .5f, data.Transform.Position.y + 1, (min.y + max.y) * .5f),
                new Vector3(max.x - min.x, 2, max.y - min.y));
            if (!bounds.IntersectRay(ray, out float distance) || distance >= best) continue;
            best = distance; picked = data;
            known = new FogKnownTarget { HasValue = true, Kind = data.Kind, Position = data.Transform.Position, Min = min, Max = max };
            if (data.Kind == FogRenderKind.Resource && fog.ResourceTypes.TryGetValue(data.Entity, out var type)) known.ResourceType = type;
        }
        return known.HasValue && picked.Kind == kind;
    }
}
