using System;
using System.Collections.Generic;
using Unity.Mathematics;

[Serializable]
public class TerrainGenerationSettings
{
    public TerrainGenerationAlgorithm algorithm;
    public uint seed = 30000;
    public int players = 4;
    public float protectedRadius = 6;
    public float landscapeHeight = 3;
    public float detailAmplitude = .8f;
    public float wavelength = 36;
    public float levelStep = 1;
    public int cliffCells = 1;
    public int rampWidth = 3;
    [UnityEngine.Min(1)] public float mountainWavelength = 19;
    [UnityEngine.Range(-1,1)] public float mountainThreshold = .20f;
    [UnityEngine.Range(0,2)] public float mountainDensity = 1;
    [UnityEngine.Min(0)] public float mountainSpawnBuffer = 4;
    [UnityEngine.Min(1)] public int mountainMinimumClusterCells = 1;
    public bool layoutClearings;
    [UnityEngine.Min(2)] public int battlefieldRadius = 14;
}

public sealed class GeneratedTerrain
{
    public GridComponent grid;
    public GridTerrain[] cells;
    public int2[] spawns;
    public int attempt;
    public int rampCount;
    public int[] sourceVertexLevels; // Shared-vertex source, retained for diagnostics before terracing.
    public readonly List<TerrainTileDraw> draws = new List<TerrainTileDraw>();
}

public struct TerrainTileDraw
{
    public int cell, tile, rotation, layer;
    // Sprite tile IDs: straight, outer, inner, single, left, middle, right, grass, soil.
}

// Managed, readable generation first. No clearance, balance gate or sprite collision.
// Uses the existing C# RNG/permutation; same seed is not promised to match JavaScript.
public static class TerrainGeneration
{
    public static readonly int2[] Directions = GridTerrain.RampDirections;
    sealed class Run
    {
        public int direction, highLevel, lowLevel;
        public readonly List<int> high = new List<int>();
    }
    sealed class Portal
    {
        public int direction;
        public readonly List<int> high = new List<int>();
        public readonly List<int> cells = new List<int>();
    }
    static uint Mix(uint x)
    {
        unchecked { x += 0x9e3779b9; x = (x ^ (x >> 16)) * 0x85ebca6b; x = (x ^ (x >> 13)) * 0xc2b2ae35; return x ^ (x >> 16); }
    }
    static float Noise(float2 p, float wavelength, uint[] permutation)
        => MapGenerator.fbm2D(p, 3, 2, 1 / wavelength, .5f, permutation) * 2 - 1;
    static int Next(int i, int d, GridComponent g)
    {
        int2 p = GridHelper.GetGridPosFromIndex(i, g) + Directions[d];
        return p.x < 0 || p.y < 0 || p.x >= g.width || p.y >= g.height ? -1 : GridHelper.GetNodeIndex(p, g);
    }
    static bool Protected(int i, GeneratedTerrain m, float radius)
    {
        float2 p = (float2)GridHelper.GetGridPosFromIndex(i, m.grid) + .5f;
        foreach (var b in m.spawns) if (math.distance(p, (float2)b + .5f) <= radius) return true;
        return false;
    }
    public static int2[] GenerateSpawnCells(GridComponent g, uint seed, int players, float radius)
    {
        float2 center = new float2((g.width - 1) * .5f, (g.height - 1) * .5f);
        float2 available = center - (radius + 4);
        if (math.any(available <= 0)) throw new ArgumentException("Grid too small for protected spawns");
        var rng = new Unity.Mathematics.Random(Mix(seed) | 1u);
        var result = new int2[players];
        float sector = 2 * math.PI / players, offset = math.PI / 4 + rng.NextFloat(-.12f, .12f);
        for (int player = 0; player < players; player++)
        {
            bool placed = false;
            for (int trial = 0; trial < 64; trial++)
            {
                float angle = offset + sector * (player + rng.NextFloat(-.12f, .12f));
                float2 dir = new float2(math.cos(angle), math.sin(angle));
                float ray = math.cmin(available / math.max(math.abs(dir), new float2(.00001f)));
                int2 cell = (int2)math.round(center + dir * ray * rng.NextFloat(.88f, .98f));
                bool legal = true;
                for (int j = 0; j < player; j++) if (math.distance((float2)cell, (float2)result[j]) < 2 * (radius + 2) + 1) legal = false;
                if (!legal) continue;
                result[player] = cell; placed = true; break;
            }
            if (!placed) throw new InvalidOperationException("Insufficient space for spawn cores");
        }
        return result;
    }
    public static GeneratedTerrain Generate(GridComponent g, TerrainGenerationSettings s)
    {
        ValidateSettings(g, s);
        if(s.algorithm==TerrainGenerationAlgorithm.SharedVertexSlopes)return SharedVertexTerrainGeneration.Generate(g,s);
        string failure = "";
        for (int attempt = 0; attempt < 16; attempt++)
        {
            uint seed = Mix(s.seed ^ (uint)(attempt + 1) * 0x45d9f3bu);
            var m = new GeneratedTerrain { grid = g, attempt = attempt, cells = new GridTerrain[g.width * g.height] };
            try { m.spawns = GenerateSpawnCells(g, seed, s.players, s.protectedRadius); }
            catch (InvalidOperationException e) { failure = e.Message; continue; }
            BuildLevelsAndMountains(m, s, seed);
            if(TryFinishTerraces(m,s,out failure))return m;
        }
        throw new InvalidOperationException("Map generation rejected all 16 candidates: " + failure);
    }
    // Both landscape sources use the same cliff/ramp topology and gameplay validation.
    public static bool TryFinishTerraces(GeneratedTerrain m,TerrainGenerationSettings s,out string failure)
    {
            failure="";
            LimitHeightSteps(m);
            BakeCliffs(m, s);
            BakeInnerCorners(m);
            List<Run> runs = StraightRuns(m);
            var reserved = new bool[m.cells.Length];
            // Connectivity repair first: only accept portals that join existing islands.
            if (!Connect(m, s, runs, reserved)) { failure = "No legal cliff portals can connect remaining islands"; return false; }
            runs = StraightRuns(m); // Pocket sealing can change which faces still exist.
            // Quota second: adding legal passages cannot split the connected walk mask.
            foreach (Run run in runs)
            {
                int required = Math.Max(1, (run.high.Count + 11) / 12);
                for (int width = Math.Min(s.rampWidth, run.high.Count); width >= 1 && OpenCount(m, run) < required; width--)
                    for (int start = 0; start + width <= run.high.Count && OpenCount(m, run) < required; start++)
                    {
                        Portal p = FindPortal(m, s, run, start, width, reserved);
                        if (p != null) Open(m, p, reserved);
                    }
            }
            if (ComponentLabels(m, out int count).Length != m.cells.Length || count != 1) { failure = "Final walk mask is disconnected"; return false; }
            try { Validate(m, s); }
            catch (InvalidOperationException e) { failure = e.Message; return false; }
            return true;
    }
    static void ValidateSettings(GridComponent g, TerrainGenerationSettings s)
    {
        if (s == null || g.width < 16 || g.height < 16 || g.width > 512 || g.height > 512 ||
            !math.isfinite(g.cellsize) || g.cellsize <= 0 || s.players < 2 || s.players > 10 ||
            !math.isfinite(s.protectedRadius) || s.protectedRadius < 1 || !math.isfinite(s.wavelength) || s.wavelength <= 0 ||
            !math.isfinite(s.levelStep) || s.levelStep <= 0 || !math.isfinite(s.landscapeHeight) || s.landscapeHeight <= 0 ||
            !math.isfinite(s.detailAmplitude) || s.detailAmplitude < 0 || s.cliffCells < 1 || s.cliffCells > 3 || s.rampWidth < 1 || s.rampWidth > 12 ||
            !math.isfinite(s.mountainWavelength) || s.mountainWavelength < 1 || !math.isfinite(s.mountainThreshold) || math.abs(s.mountainThreshold)>1 ||
            !math.isfinite(s.mountainDensity) || s.mountainDensity<0 || s.mountainDensity>2 || !math.isfinite(s.mountainSpawnBuffer) || s.mountainSpawnBuffer<0 ||
            s.mountainMinimumClusterCells<1 || s.mountainMinimumClusterCells>4096 || s.battlefieldRadius<2 || s.battlefieldRadius>64)
            throw new ArgumentException("Invalid grid or terrain settings");
    }
    static void BuildLevelsAndMountains(GeneratedTerrain m, TerrainGenerationSettings s, uint seed)
    {
        var boundary = MapGenRNG.GetPermatureList(seed ^ 0x12345);
        var detail = MapGenRNG.GetPermatureList(seed ^ 0x67890);
        var rocks = MapGenRNG.GetPermatureList(seed ^ 0xf001);
        float scale = Math.Min(m.grid.width, m.grid.height);
        Func<float2, float> landscape = p =>
        {
            float2 warp = p + 14 * new float2(Noise(p + new float2(81, -39), 75, boundary), Noise(p + new float2(-53, 92), 75, boundary));
            return s.landscapeHeight * (.65f + 1.5f * Noise(warp + new float2(217, 151), scale * .38f, boundary) +
                .4f * (1 - math.abs(2 * Noise(warp + new float2(-93, 163), scale * .23f, rocks))));
        };
        var baseHeights = new float[m.spawns.Length];
        for (int b = 0; b < baseHeights.Length; b++) baseHeights[b] = landscape((float2)m.spawns[b] + .5f) + s.landscapeHeight * .24f;
        // Sample vertices so each cell quantises the average of four corners.
        int stride = m.grid.width + 1;
        var raw = new float[stride * (m.grid.height + 1)];
        for (int y = 0; y <= m.grid.height; y++) for (int x = 0; x <= m.grid.width; x++)
        {
            float2 p = new float2(x, y); float h = landscape(p), detailMask = 1;
            for (int b = 0; b < m.spawns.Length; b++)
            {
                float r = math.distance(p, (float2)m.spawns[b] + .5f);
                float outer = s.protectedRadius + 13 + 3 * Noise(p + new float2(47, -38), 28, boundary);
                float weight = 1 - MapGenerator.fade(math.saturate((r - s.protectedRadius) / (outer - s.protectedRadius)));
                h = math.lerp(h, baseHeights[b], weight); detailMask = Math.Min(detailMask, 1 - weight);
            }
            raw[GridHelper.GetNodeIndex(new int2(x, y), stride)] = math.round((h + s.detailAmplitude * detailMask * Noise(p, s.wavelength, detail)) * 256) / 256;
        }
        for (int i = 0; i < m.cells.Length; i++)
        {
            int2 p = GridHelper.GetGridPosFromIndex(i, m.grid); int v = GridHelper.GetNodeIndex(p, stride);
            m.cells[i] = new GridTerrain { heightLevel = (int)math.floor((raw[v] + raw[v + 1] + raw[v + stride] + raw[v + stride + 1]) / (4 * s.levelStep) + .5f), walkable = true, RampId = -1, RampDirection = -1 };
        }
        for (int pass = 0; pass < 3; pass++)
        {
            var old = (GridTerrain[])m.cells.Clone();
            for (int y = 1; y < m.grid.height - 1; y++) for (int x = 1; x < m.grid.width - 1; x++)
            {
                int i = GridHelper.GetNodeIndex(new int2(x, y), m.grid); var votes = new Dictionary<int, int>();
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                { int l = old[GridHelper.GetNodeIndex(new int2(x + dx, y + dy), m.grid)].heightLevel; votes[l] = votes.TryGetValue(l, out int n) ? n + 1 : 1; }
                int best = old[i].heightLevel, count = votes[best];
                foreach (var vote in votes) if (vote.Value > count) { best = vote.Key; count = vote.Value; }
                m.cells[i].heightLevel = best;
            }
        }
        // A bounded retry fallback makes straight interfaces without carving mountains.
        if (m.attempt >= 8)
        {
            int step = Math.Max(6, s.rampWidth + 3);
            for (int by = 0; by < m.grid.height; by += step) for (int bx = 0; bx < m.grid.width; bx += step)
            {
                var values = new List<int>();
                for (int y = by; y < Math.Min(by + step, m.grid.height); y++) for (int x = bx; x < Math.Min(bx + step, m.grid.width); x++) values.Add(m.cells[GridHelper.GetNodeIndex(new int2(x, y), m.grid)].heightLevel);
                values.Sort(); int level = values[values.Count / 2];
                for (int y = by; y < Math.Min(by + step, m.grid.height); y++) for (int x = bx; x < Math.Min(bx + step, m.grid.width); x++) m.cells[GridHelper.GetNodeIndex(new int2(x, y), m.grid)].heightLevel = level;
            }
        }
        for (int i = 0; i < m.cells.Length; i++)
        {
            float2 p = (float2)GridHelper.GetGridPosFromIndex(i, m.grid) + .5f;
            // Extra border keeps high-side cliffs and diagonal corners outside the protected core.
            for (int b = 0; b < m.spawns.Length; b++) if (math.distance(p, (float2)m.spawns[b] + .5f) <= s.protectedRadius + 4)
                m.cells[i].heightLevel = (int)math.floor(baseHeights[b] / s.levelStep + .5f);
        }
        BuildMountainClusters(m,s,seed);
    }
    // Shared by both height sources; preserves the legacy noise, gap fill and component levels.
    public static void BuildMountainClusters(GeneratedTerrain m,TerrainGenerationSettings s,uint seed)
    {
        bool[] clearings = BuildLayoutClearings(m,s);
        var rocks=MapGenRNG.GetPermatureList(seed ^ 0xf001);
        for(int i=0;i<m.cells.Length;i++){
            float2 p=(float2)GridHelper.GetGridPosFromIndex(i,m.grid)+.5f;
            m.cells[i].isMountain=s.mountainDensity>0 && !clearings[i] && !Protected(i,m,s.protectedRadius+s.mountainSpawnBuffer)&&Noise(p+new float2(79,-143),s.mountainWavelength,rocks)>s.mountainThreshold+(1-s.mountainDensity)*.35f;
        }
        var original = (GridTerrain[])m.cells.Clone();
        for (int i = 0; i < m.cells.Length; i++) if (!original[i].isMountain && !clearings[i] && !Protected(i, m, s.protectedRadius + s.mountainSpawnBuffer))
        {
            for (int d = 0; d < 2; d++) { int a = Next(i, d, m.grid), b = Next(i, d + 2, m.grid); if (a >= 0 && b >= 0 && original[a].isMountain && original[b].isMountain) m.cells[i].isMountain = true; }
        }
        if(s.mountainMinimumClusterCells>1) {
            var seen=new bool[m.cells.Length];
            for(int root=0;root<m.cells.Length;root++)if(m.cells[root].isMountain&&!seen[root]) {
                var group=new List<int>{root};seen[root]=true;
                for(int k=0;k<group.Count;k++)for(int d=0;d<4;d++){int j=Next(group[k],d,m.grid);if(j>=0&&m.cells[j].isMountain&&!seen[j]){seen[j]=true;group.Add(j);}}
                if(group.Count<s.mountainMinimumClusterCells)foreach(int i in group)m.cells[i].isMountain=false;
            }
        }
        UnifyMountainLevels(m);
    }
    static bool[] BuildLayoutClearings(GeneratedTerrain m,TerrainGenerationSettings s)
    {
        var mask=new bool[m.cells.Length];if(!s.layoutClearings)return mask;
        // Five bounded arenas, separated by the existing noisy terraces and mountain corridors.
        // Spawn cores take priority, and the normal portal/connectivity checks still run afterwards.
        var center=new int2(m.grid.width/2,m.grid.height/2);
        int radius=Math.Min(s.battlefieldRadius,Math.Min(m.grid.width,m.grid.height)/10);
        var sites=new[]{center,center+new int2(m.grid.width/5,0),center-new int2(m.grid.width/5,0),center+new int2(0,m.grid.height/5),center-new int2(0,m.grid.height/5)};
        foreach(var site in sites){
            var levels=new List<int>();
            for(int z=site.y-radius;z<=site.y+radius;z++)for(int x=site.x-radius;x<=site.x+radius;x++)
                if(x>=0&&z>=0&&x<m.grid.width&&z<m.grid.height&&math.distancesq(new int2(x,z),site)<=radius*radius)levels.Add(m.cells[z*m.grid.width+x].heightLevel);
            levels.Sort();int height=levels[levels.Count/2];
            for(int z=site.y-radius;z<=site.y+radius;z++)for(int x=site.x-radius;x<=site.x+radius;x++) {
                if(x<0||z<0||x>=m.grid.width||z>=m.grid.height||math.distancesq(new int2(x,z),site)>radius*radius)continue;
                int i=z*m.grid.width+x;if(Protected(i,m,s.protectedRadius+4))continue;
                mask[i]=true;m.cells[i].heightLevel=height;
            }
        }
        return mask;
    }
    static void UnifyMountainLevels(GeneratedTerrain m)
    {
        var visited = new bool[m.cells.Length];
        for (int root = 0; root < m.cells.Length; root++) if (m.cells[root].isMountain && !visited[root])
        {
            var component = new List<int> { root }; visited[root] = true;
            for (int k = 0; k < component.Count; k++) for (int d = 0; d < 4; d++)
            { int j = Next(component[k], d, m.grid); if (j >= 0 && m.cells[j].isMountain && !visited[j]) { visited[j] = true; component.Add(j); } }
            var levels = component.ConvertAll(i => m.cells[i].heightLevel); levels.Sort();
            foreach (int i in component) { m.cells[i].heightLevel = levels[levels.Count / 2]; m.cells[i].walkable = false; }
        }
    }
    public static void LimitHeightSteps(GeneratedTerrain m)
    {
        // Same quantised legacy landscape; monotone relaxation changes metadata only.
        var queue = new Queue<int>(); var queued = new bool[m.cells.Length];
        for(int i=0;i<m.cells.Length;i++) if(!m.cells[i].isMountain){queue.Enqueue(i);queued[i]=true;}
        while(queue.Count>0) {
            int i=queue.Dequeue();queued[i]=false;
            int2 p=GridHelper.GetGridPosFromIndex(i,m.grid);
            for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++) {
                if(dx==0&&dy==0)continue;int2 q=p+new int2(dx,dy);
                if(q.x<0||q.y<0||q.x>=m.grid.width||q.y>=m.grid.height)continue;
                int j=GridHelper.GetNodeIndex(q,m.grid);
                if(m.cells[j].isMountain||m.cells[j].heightLevel<=m.cells[i].heightLevel+1)continue;
                m.cells[j].heightLevel=m.cells[i].heightLevel+1;
                if(!queued[j]){queue.Enqueue(j);queued[j]=true;}
            }
        }
    }
    static void BakeCliffs(GeneratedTerrain m, TerrainGenerationSettings s)
    {
        for (int i = 0; i < m.cells.Length; i++) if (!m.cells[i].isMountain) for (int d = 1; d <= 2; d++)
        {
            int j = Next(i, d, m.grid); if (j < 0 || m.cells[j].isMountain) continue;
            int delta = Math.Abs(m.cells[i].heightLevel - m.cells[j].heightLevel); if (delta == 0) continue;
            int high = m.cells[i].heightLevel > m.cells[j].heightLevel ? i : j;
            int2 p = GridHelper.GetGridPosFromIndex(high, m.grid); int depth = delta * s.cliffCells;
            for (int dy = 1 - depth; dy < depth; dy++) for (int dx = 1 - depth; dx < depth; dx++)
            {
                if (Math.Abs(dx) + Math.Abs(dy) >= depth) continue;
                int2 q = p + new int2(dx, dy); if (q.x < 0 || q.y < 0 || q.x >= m.grid.width || q.y >= m.grid.height) continue;
                int k = GridHelper.GetNodeIndex(q, m.grid);
                if (m.cells[k].isMountain || m.cells[k].heightLevel != m.cells[high].heightLevel) continue;
                m.cells[k].isCliff = true; m.cells[k].walkable = false;
            }
        }
    }
    static void BakeInnerCorners(GeneratedTerrain m)
    {
        for (int i = 0; i < m.cells.Length; i++)
        {
            if (m.cells[i].isMountain || LowerMask(m, i) != 0) continue;
            for (int d = 0; d < 4; d++)
            {
                int a = Next(i, d, m.grid), b = Next(i, (d + 1) % 4, m.grid);
                int diagonal = a < 0 ? -1 : Next(a, (d + 1) % 4, m.grid);
                if (b < 0 || diagonal < 0 || m.cells[a].isMountain || m.cells[b].isMountain || m.cells[diagonal].isMountain) continue;
                int level = m.cells[i].heightLevel;
                if (m.cells[a].heightLevel == level && m.cells[b].heightLevel == level && m.cells[diagonal].heightLevel < level)
                { m.cells[i].isCliff = true; m.cells[i].walkable = false; }
            }
        }
    }
    public static int LowerMask(GeneratedTerrain m, int i)
    {
        int mask = 0;
        for (int d = 0; d < 4; d++) { int j = Next(i, d, m.grid); if (j >= 0 && !m.cells[j].isMountain && m.cells[j].heightLevel < m.cells[i].heightLevel) mask |= 1 << d; }
        return mask;
    }
    static List<Run> StraightRuns(GeneratedTerrain m)
    {
        var directions = new int[m.cells.Length]; Array.Fill(directions, -1);
        for (int i = 0; i < m.cells.Length; i++) if (!m.cells[i].isMountain)
        {
            int mask = LowerMask(m, i); if (mask == 0 || (mask & (mask - 1)) != 0) continue;
            int d = 0; while ((mask & (1 << d)) == 0) d++;
            int low = Next(i, d, m.grid); if (low >= 0 && m.cells[i].isCliff) directions[i] = d;
        }
        var seen = new bool[m.cells.Length]; var runs = new List<Run>();
        for (int i = 0; i < m.cells.Length; i++) if (directions[i] >= 0 && !seen[i])
        {
            int d = directions[i]; var run = new Run { direction = d, highLevel = m.cells[i].heightLevel, lowLevel = m.cells[Next(i, d, m.grid)].heightLevel };
            run.high.Add(i); seen[i] = true;
            for (int k = 0; k < run.high.Count; k++) foreach (int tangent in new[] { (d + 1) % 4, (d + 3) % 4 })
            {
                int j = Next(run.high[k], tangent, m.grid);
                if (j < 0 || seen[j] || directions[j] != d || m.cells[j].heightLevel != run.highLevel || m.cells[Next(j, d, m.grid)].heightLevel != run.lowLevel) continue;
                seen[j] = true; run.high.Add(j);
            }
            int2 t = Directions[(d + 1) % 4];
            run.high.Sort((a, b) => math.dot(GridHelper.GetGridPosFromIndex(a, m.grid), t).CompareTo(math.dot(GridHelper.GetGridPosFromIndex(b, m.grid), t)));
            runs.Add(run);
        }
        return runs;
    }
    static Portal FindPortal(GeneratedTerrain m, TerrainGenerationSettings s, Run r, int start, int width, bool[] reserved)
    {
        if(r.highLevel-r.lowLevel != 1) return null;
        var portal = new Portal { direction = (r.direction + 2) % 4 };
        for (int lane = start; lane < start + width; lane++)
        {
            int high = r.high[lane], low = Next(high, r.direction, m.grid);
            if (!m.cells[low].walkable || reserved[high] || reserved[low] || Protected(low, m, s.protectedRadius + 2) || m.cells[low].RampId >= 0) return null;
            portal.high.Add(high); portal.cells.Add(low); bool landed = false;
            for (int depth = 0, j = high; depth < (r.highLevel - r.lowLevel) * s.cliffCells + 3; depth++, j = Next(j, portal.direction, m.grid))
            {
                if (j < 0 || m.cells[j].isMountain || m.cells[j].heightLevel != r.highLevel || m.cells[j].RampId >= 0 || reserved[j] || Protected(j, m, s.protectedRadius + 2)) return null;
                portal.cells.Add(j);
                if (!m.cells[j].isCliff) { landed = m.cells[j].walkable; break; }
                // No newly opened cell may create a crossing through a corner face.
                for (int d = 0; d < 4; d++)
                {
                    int other = Next(j, d, m.grid); if (other < 0 || !m.cells[other].walkable || m.cells[other].heightLevel == r.highLevel) continue;
                    if (j != high || other != low) return null;
                }
            }
            if (!landed) return null;
        }
        return portal;
    }
    static void Open(GeneratedTerrain m, Portal p, bool[] reserved)
    {
        int id = m.rampCount++;
        foreach (int i in p.cells) { m.cells[i].walkable = true; m.cells[i].RampId = id; }
        foreach (int high in p.high)
        {
            int low = Next(high, (p.direction + 2) % 4, m.grid); m.cells[low].RampDirection = p.direction; m.cells[high].RampDirection = p.direction;
            foreach (int i in new[] { high, low })
            {
                int2 c = GridHelper.GetGridPosFromIndex(i, m.grid);
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                { int2 q = c + new int2(dx, dy); if (q.x >= 0 && q.y >= 0 && q.x < m.grid.width && q.y < m.grid.height) reserved[GridHelper.GetNodeIndex(q, m.grid)] = true; }
            }
        }
    }
    static int OpenCount(GeneratedTerrain m, Run r)
    {
        int count = 0; foreach (int high in r.high) { int low = Next(high, r.direction, m.grid); if (m.cells[high].walkable && m.cells[low].walkable && m.cells[low].RampId >= 0) count++; } return count;
    }
    public static int[] ComponentLabels(GeneratedTerrain m, out int count)
    {
        var labels = new int[m.cells.Length]; var queue = new Queue<int>(); count = 0;
        for (int i = 0; i < labels.Length; i++) if (m.cells[i].walkable && labels[i] == 0)
        {
            labels[i] = ++count; queue.Enqueue(i);
            while (queue.Count > 0) { int k = queue.Dequeue(); for (int d = 0; d < 4; d++) { int j = Next(k, d, m.grid); if (j >= 0 && m.cells[j].walkable && labels[j] == 0) { labels[j] = count; queue.Enqueue(j); } } }
        }
        return labels;
    }
    static bool Connect(GeneratedTerrain m, TerrainGenerationSettings s, List<Run> runs, bool[] reserved)
    {
        var labels = ComponentLabels(m, out int count);
        var parent = new int[count + 1]; for (int i = 0; i < parent.Length; i++) parent[i] = i;
        Func<int, int> root = null; root = x => { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; };
        for (int width = s.rampWidth; width >= 1; width--) foreach (Run run in runs)
            for (int start = 0; start + width <= run.high.Count; start++)
            {
                Portal p = FindPortal(m, s, run, start, width, reserved); if (p == null) continue;
                var touched = new HashSet<int>();
                foreach (int i in p.cells) { if (labels[i] > 0) touched.Add(root(labels[i])); for (int d = 0; d < 4; d++) { int j = Next(i, d, m.grid); if (j >= 0 && labels[j] > 0) touched.Add(root(labels[j])); } }
                if (touched.Count < 2) continue;
                int target = 0; foreach (int label in touched) { if (target == 0) target = label; else parent[root(label)] = root(target); }
                Open(m, p, reserved); foreach (int i in p.cells) labels[i] = target;
            }
        labels = ComponentLabels(m, out count); if (count == 1) return true;
        int main = labels[GridHelper.GetNodeIndex(m.spawns[0], m.grid)]; if (main == 0) return false;
        var sizes = new int[count + 1]; foreach (int label in labels) sizes[label]++;
        foreach (int2 spawn in m.spawns) if (labels[GridHelper.GetNodeIndex(spawn, m.grid)] != main) return false;
        int fill = 0; for (int label = 1; label <= count; label++) if (label != main) { if (sizes[label] > 64) return false; fill += sizes[label]; }
        if (fill > m.cells.Length * .02f) return false;
        for (int i = 0; i < labels.Length; i++) if (labels[i] != 0 && labels[i] != main)
        { m.cells[i].isMountain = true; m.cells[i].walkable = false; m.cells[i].isCliff = false; m.cells[i].RampId = -1; m.cells[i].RampDirection = -1; }
        UnifyMountainLevels(m);
        return true;
    }
    static void Validate(GeneratedTerrain m, TerrainGenerationSettings s)
    {
        var labels = ComponentLabels(m, out int count); if (count != 1) throw new InvalidOperationException("Expected one walk island");
        foreach (int2 spawn in m.spawns)
        {
            int i = GridHelper.GetNodeIndex(spawn, m.grid), level = m.cells[i].heightLevel;
            if (labels[i] == 0) throw new InvalidOperationException("Blocked spawn");
            for (int j = 0; j < m.cells.Length; j++) if (math.distance((float2)GridHelper.GetGridPosFromIndex(j, m.grid) + .5f, (float2)spawn + .5f) <= s.protectedRadius + 2 &&
                (m.cells[j].heightLevel != level || m.cells[j].isMountain || !m.cells[j].walkable)) throw new InvalidOperationException("Spawn core is not protected");
        }
        var faceOwners = new int[m.cells.Length]; Array.Fill(faceOwners, -1);
        for (int i = 0; i < m.cells.Length; i++)
        {
            if (m.cells[i].isMountain && (m.cells[i].walkable || m.cells[i].RampId >= 0)) throw new InvalidOperationException("Mountain carved");
            if (m.cells[i].isCliff && m.cells[i].walkable && m.cells[i].RampId < 0) throw new InvalidOperationException("Unmarked cliff opening");
            if (!m.cells[i].walkable) continue;
            for (int d = 0; d < 4; d++)
            {
                int j = Next(i, d, m.grid); if (j < 0 || !m.cells[j].walkable || m.cells[i].heightLevel == m.cells[j].heightLevel) continue;
                if(Math.Abs(m.cells[i].heightLevel-m.cells[j].heightLevel)!=1) throw new InvalidOperationException("Ramp must cross exactly one tier");
                int high = m.cells[i].heightLevel > m.cells[j].heightLevel ? i : j, mask = LowerMask(m, high);
                if (m.cells[i].RampId < 0 || m.cells[j].RampId < 0 || mask == 0 || (mask & (mask - 1)) != 0) throw new InvalidOperationException("Illegal tier crossing");
                int low = high == i ? j : i;
                faceOwners[high] = m.cells[low].RampId; faceOwners[low] = m.cells[low].RampId;
            }
        }
        for (int i = 0; i < faceOwners.Length; i++) if (faceOwners[i] >= 0)
        {
            int2 p = GridHelper.GetGridPosFromIndex(i, m.grid);
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
            {
                int2 q = p + new int2(dx, dy); if (q.x < 0 || q.y < 0 || q.x >= m.grid.width || q.y >= m.grid.height) continue;
                int owner = faceOwners[GridHelper.GetNodeIndex(q, m.grid)];
                if (owner >= 0 && owner != faceOwners[i]) throw new InvalidOperationException("Ramp groups require one-cell separation");
            }
        }
    }
}
