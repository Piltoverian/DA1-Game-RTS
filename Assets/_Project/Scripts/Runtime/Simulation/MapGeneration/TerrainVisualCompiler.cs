using System;
using System.Collections.Generic;
using Unity.Mathematics;

// Visuals read terrain only. Collision cliffs and sprite faces live on HIGH cells.
public static class TerrainVisualCompiler
{
    static int Next(int i, int d, GridComponent g)
    {
        int2 p = GridHelper.GetGridPosFromIndex(i, g) + GridTerrain.RampDirections[d];
        return p.x < 0 || p.y < 0 || p.x >= g.width || p.y >= g.height ? -1 : GridHelper.GetNodeIndex(p, g);
    }
    static int Rotate(int mask, int r) => ((mask << r) | (mask >> (4 - r))) & 15;
    static void AddPart(GeneratedTerrain m, int cell, int tile, int highMask)
    {
        int canonical = tile == 0 ? 3 : tile == 1 ? 1 : 14;
        for (int r = 0; r < 4; r++) if (Rotate(canonical, r) == highMask)
        { m.draws.Add(new TerrainTileDraw { cell = cell, tile = tile, rotation = r, layer = 1 }); return; }
        throw new InvalidOperationException("Unrecognised cliff high-side mask");
    }
    public static void Compile(GeneratedTerrain m)
    {
        m.draws.Clear();
        var ramps = new Dictionary<int, List<int>>();
        var slopeDirection = new int[m.cells.Length]; Array.Fill(slopeDirection, -1);
        // Identify the exact low/high crossing, not all cells in the ramp corridor.
        for (int low = 0; low < m.cells.Length; low++)
        {
            var c = m.cells[low]; if (!c.walkable || c.RampId < 0) continue;
            for (int d = 0; d < 4; d++)
            {
                int high = Next(low, d, m.grid);
                if (high < 0 || !m.cells[high].walkable || m.cells[high].heightLevel <= c.heightLevel) continue;
                int mask = TerrainGeneration.LowerMask(m, high);
                if (mask != (1 << ((d + 2) % 4))) throw new InvalidOperationException("Ramp occupies a cliff corner");
                if (slopeDirection[high] >= 0) throw new InvalidOperationException("Overlapping ramp face");
                slopeDirection[high] = d;
                if (!ramps.TryGetValue(c.RampId, out var group)) ramps[c.RampId] = group = new List<int>();
                group.Add(high);
            }
        }
        foreach (var pair in ramps)
        {
            List<int> cells = pair.Value; int d = slopeDirection[cells[0]];
            int2 tangent = GridTerrain.RampDirections[(d + 1) % 4];
            cells.Sort((a, b) => math.dot(GridHelper.GetGridPosFromIndex(a, m.grid), tangent).CompareTo(math.dot(GridHelper.GetGridPosFromIndex(b, m.grid), tangent)));
            for (int k = 0; k < cells.Count; k++)
            {
                if (slopeDirection[cells[k]] != d) throw new InvalidOperationException("Ramp group has inconsistent directions");
                int tile = cells.Count == 1 ? 3 : k == 0 ? 4 : k == cells.Count - 1 ? 6 : 5;
                m.draws.Add(new TerrainTileDraw { cell = cells[k], tile = tile, rotation = d, layer = 2 });
            }
        }
        int[] straight = { 12, 9, 3, 6 };
        int[] convex = new int[16]; convex[3] = 8; convex[6] = 1; convex[12] = 2; convex[9] = 4;
        int[] dx = { -1, 1, 1, -1 }, dy = { -1, -1, 1, 1 };
        for (int i = 0; i < m.cells.Length; i++)
        {
            if (m.cells[i].isMountain || slopeDirection[i] >= 0) continue;
            int mask = TerrainGeneration.LowerMask(m, i), normals = 0;
            for (int d = 0; d < 4; d++) if ((mask & (1 << d)) != 0) normals++;
            if (normals == 2 && convex[mask] != 0) AddPart(m, i, 1, convex[mask]);
            else if (normals > 0)
            { for (int d = 0; d < 4; d++) if ((mask & (1 << d)) != 0) AddPart(m, i, 0, straight[d]); }
            else
            {
                int2 p = GridHelper.GetGridPosFromIndex(i, m.grid);
                for (int corner = 0; corner < 4; corner++)
                {
                    int2 q = p + new int2(dx[corner], dy[corner]);
                    if (q.x < 0 || q.y < 0 || q.x >= m.grid.width || q.y >= m.grid.height) continue;
                    int j = GridHelper.GetNodeIndex(q, m.grid), a = Next(i, dx[corner] > 0 ? 1 : 3, m.grid), b = Next(i, dy[corner] > 0 ? 2 : 0, m.grid);
                    if (!m.cells[j].isMountain && a >= 0 && b >= 0 && !m.cells[a].isMountain && !m.cells[b].isMountain &&
                        m.cells[j].heightLevel < m.cells[i].heightLevel && m.cells[a].heightLevel == m.cells[i].heightLevel && m.cells[b].heightLevel == m.cells[i].heightLevel)
                        AddPart(m, i, 2, 15 ^ (1 << corner));
                }
            }
        }
        m.draws.Sort((a, b) => a.layer != b.layer ? a.layer.CompareTo(b.layer) : a.cell.CompareTo(b.cell));
    }
}
