using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

// Plain values only: a receiver can draw one cell without reading any other cell.
// Managed render data stays local. The buffers below carry its network representation.
public sealed class TerrainCellVisualData
{
    public float4 CornerHeights;
    public float TopHeight;
    public byte TopMask;
    public bool IsMountain;
    public float MountainHeight;
    public byte MountainMask;
    public TerrainWallVisualData[] Walls;

    // Server: append a newly explored cell to this connection's terrain Ghost.
    // Call once per cell, or clear BOTH buffers before writing a complete snapshot.
    // The caller must check the player's FOW; this method does not authorize visibility.
    public void AppendToGhostBuffers(int cellIndex,
        DynamicBuffer<TerrainCellGhostElement> cells,
        DynamicBuffer<TerrainWallVisualData> walls, bool visible = false)
    {
        int wallStart = walls.Length;
        if (Walls != null)
            foreach (var wall in Walls) walls.Add(wall);

        cells.Add(new TerrainCellGhostElement
        {
            CellIndex = cellIndex,
            CornerHeights = CornerHeights,
            TopHeight = TopHeight,
            TopMask = TopMask,
            IsMountain = IsMountain,
            MountainHeight = MountainHeight,
            MountainMask = MountainMask,
            WallStart = wallStart,
            WallCount = walls.Length - wallStart,
            Visible = visible
        });
    }
}

// Bake both buffers on the terrain Ghost prefab and give its root a GhostOwner.
// Assign GhostOwner.NetworkId from the authenticated connection on the SERVER.
// OwnerSendType limits these buffers' data; it does not hide the Ghost entity itself.
[GhostComponent(OwnerSendType = SendToOwnerType.SendToOwner)]
[InternalBufferCapacity(0)]
public struct TerrainCellGhostElement : IBufferElementData
{
    [GhostField] public int CellIndex;
    [GhostField(Quantization = 0)] public float4 CornerHeights;
    [GhostField(Quantization = 0)] public float TopHeight;
    [GhostField] public byte TopMask;
    [GhostField] public bool IsMountain;
    [GhostField(Quantization = 0)] public float MountainHeight;
    [GhostField] public byte MountainMask;
    // Range in the TerrainWallVisualData buffer on the SAME Ghost entity.
    [GhostField] public int WallStart;
    [GhostField] public int WallCount;
    [GhostField] public bool Visible;
}

[GhostComponent(OwnerSendType = SendToOwnerType.SendToOwner)]
[InternalBufferCapacity(0)]
public struct TerrainWallVisualData : IBufferElementData
{
    [GhostField] public byte Side;
    [GhostField] public byte Mask;
    [GhostField(Quantization = 0)] public float Height;
    [GhostField(Quantization = 0)] public float SortHeight;
}

public static class TerrainCellVisualCompiler
{
    // The producer has the complete map. Compile before applying the viewer's FOW filter.
    public static TerrainCellVisualData[] Compile(GeneratedTerrain map, float rise,
        float mountainRise, int mountainTiers, int visualSeed)
    {
        if (!(rise > 0) || !math.isfinite(rise) || !(mountainRise > 0) || !math.isfinite(mountainRise))
            throw new ArgumentException("Terrain visual tier heights must be finite and positive");
        var surface = new TerrainSurfaceData(map, rise);
        var mountains = MountainVisualField.BuildNatural(map, mountainTiers, visualSeed);
        var result = new TerrainCellVisualData[map.cells.Length];
        for (int z = 0; z < map.grid.height; z++)
        for (int x = 0; x < map.grid.width; x++)
        {
            int i = z * map.grid.width + x;
            float4 h = surface.Corners(i);
            float bottom = math.cmin(h);
            int mask = 0;
            for (int k = 0; k < 4; k++)
            {
                float tier = (h[k] - bottom) / rise;
                if (tier > 1.001f) throw new InvalidOperationException("Top cell spans more than one sprite tier");
                if (tier > .5f) mask |= 1 << k;
            }
            var walls = new List<TerrainWallVisualData>();
            for (int side = 0; side < 2; side++)
            {
                int j = side == 0 ? (z > 0 ? i - map.grid.width : -1) : (x > 0 ? i - 1 : -1);
                float a = h[0], b = h[side == 0 ? 1 : 3], oa = 0, ob = 0;
                if (j >= 0)
                {
                    oa = surface.CellHeight(j, side == 0 ? 0 : 1, side == 0 ? 1 : 0);
                    ob = surface.CellHeight(j, 1, 1);
                }
                float loA = math.min(a, oa), loB = math.min(b, ob);
                if (a <= loA + .001f && b <= loB + .001f) continue;
                int first = (int)math.floor(math.min(loA, loB) / rise + .0001f);
                int last = (int)math.ceil(math.max(a, b) / rise - .0001f);
                for (int tier = first; tier < last; tier++)
                {
                    float height = tier * rise;
                    float4 e = math.saturate((new float4(loA, loB, b, a) - height) / rise);
                    if (e[3] <= e[0] + .001f && e[2] <= e[1] + .001f) continue;
                    int edgeMask = 0;
                    for (int k = 0; k < 4; k++) if (e[k] > .5f) edgeMask |= 1 << k;
                    if (!TerrainTheme.IsVisibleWallMask(edgeMask)) continue;
                    walls.Add(new TerrainWallVisualData
                    {
                        Side = (byte)side, Mask = (byte)edgeMask, Height = height,
                        SortHeight = height + (e[0] + e[1] + e[2] + e[3]) * .25f * rise
                    });
                }
            }
            var cell = new TerrainCellVisualData
            {
                CornerHeights = h, TopHeight = bottom, TopMask = (byte)mask,
                IsMountain = map.cells[i].isMountain, Walls = walls.ToArray()
            };
            if (cell.IsMountain)
            {
                int stride = map.grid.width + 1, a = z * stride + x;
                int4 levels = new int4(mountains[a], mountains[a + 1], mountains[a + stride + 1], mountains[a + stride]);
                int low = math.cmin(levels), rockMask = 0;
                for (int k = 0; k < 4; k++)
                {
                    if (levels[k] - low > 1) throw new InvalidOperationException("Mountain cap exceeds one tier");
                    if (levels[k] > low) rockMask |= 1 << k;
                }
                cell.MountainHeight = bottom + low * mountainRise;
                cell.MountainMask = (byte)rockMask;
            }
            result[i] = cell;
        }
        return result;
    }
}
