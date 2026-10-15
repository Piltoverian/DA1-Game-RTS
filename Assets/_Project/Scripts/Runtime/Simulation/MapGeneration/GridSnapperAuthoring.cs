using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
public class GridSnapperAuthoring : MonoBehaviour
{
    [Min(1), Tooltip("Square footprint in grid cells: 7 means exactly 7 by 7 cells.")]
    public int GridScale = 7;

    class Baker : Baker<GridSnapperAuthoring>
    {
        public override void Bake(GridSnapperAuthoring authoring)
        {
            var box = GetComponent<BoxCollider>();
            var transform = GetComponent<Transform>();
            if (authoring.GridScale < 1 || box == null || transform.parent != null ||
                Quaternion.Angle(transform.rotation, Quaternion.identity) > .01f ||
                math.any((float3)transform.lossyScale <= 0))
                throw new InvalidOperationException("Grid snapper needs a positive size, root BoxCollider, positive scale and unrotated root.");
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            var visual = ModelSnapSource.CaptureVisualBounds(authoring.gameObject, new Bounds(box.center, box.size));
            AddComponent(entity, new GridFootprint { Cells = authoring.GridScale });
            if(!GetComponent<MovementAgentAuthoring>()&&!GetComponent<ModelSnapperAuthoring>()){
                AddComponent(entity,ModelSnapSource.Capture(authoring.gameObject,authoring.GridScale,0));
                AddComponent<Snapping>(entity);
            }
            AddComponent(entity, new GridSnapperSource
            {
                Size = (float3)box.size * (float3)transform.lossyScale,
                Center = (float3)box.center * (float3)transform.lossyScale,
                RootScale = transform.lossyScale,
                VisualSize = (float3)visual.size * (float3)transform.lossyScale,
                VisualCenter = (float3)visual.center * (float3)transform.lossyScale
            });
        }
    }
}

[BakingType]
public struct GridSnapperSource : IComponentData
{
    public float3 Size;
    public float3 Center;
    public float3 RootScale;
    public float3 VisualSize, VisualCenter;
}

public struct GridFootprint : IComponentData
{
    public int Cells;
    public float WorldSize;
}

public static class GridSnapperMath
{
    public static float3 Scale(float3 sourceSize, int cells, float cellSize)
    {
        return Scale(sourceSize, cells * cellSize);
    }

    public static float3 Scale(float3 sourceSize, float side)
    {
        if (!(side > 0) || !math.isfinite(side) || !math.all(math.isfinite(sourceSize)) || math.any(sourceSize.xz <= 0))
            throw new InvalidOperationException("Snapping needs positive finite horizontal visual bounds and target size.");
        // Fit the largest horizontal visual extent; preserve the model's proportions.
        return new float3(side / math.max(sourceSize.x, sourceSize.z));
    }

    public static float3 Snap(float3 position, GridComponent grid, int cells)
    {
        var result = GridHelper.GridToWorld(GridHelper.WorldToGrid(position, grid), grid);
        // Even footprints are centered on a grid intersection; odd footprints on a cell center.
        if ((cells & 1) == 0) result -= new float3(grid.cellsize * .5f, 0, grid.cellsize * .5f);
        result.y = position.y;
        return result;
    }
}
