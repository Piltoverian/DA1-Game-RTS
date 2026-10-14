using System;
using System.Collections.Generic;
using Unity.Mathematics;

[Serializable]
public class TerrainGenerationSettings
{
    public uint seed = 30000;
    public int players = 4;
    public float protectedRadius = 6;
    public float landscapeHeight = 3;
    public float detailAmplitude = .8f;
    public float wavelength = 36;
    public float levelStep = 1;
    public int cliffCells = 1;
    [UnityEngine.HideInInspector] public int rampWidth = 3; // Serialized block-fallback spacing, not the new portal limit.
    [UnityEngine.Min(1)] public int maximumRampCells = 12;
    [UnityEngine.Range(0, .1f)] public float smallIslandFillBudget = .02f;
    [UnityEngine.Min(1)] public float mountainWavelength = 19;
    [UnityEngine.Range(-1,1)] public float mountainThreshold = .20f;
    [UnityEngine.Range(0,2)] public float mountainDensity = 1;
    [UnityEngine.Min(0)] public float mountainSpawnBuffer = 4;
    [UnityEngine.Min(1)] public int mountainMinimumClusterCells = 1;
    public bool layoutClearings;
    [NonSerialized] public bool measureGeneration; // Explicit benchmark only; no RNG/topology changes.
    [UnityEngine.Min(2)] public int battlefieldRadius = 14;
}

public sealed class GeneratedTerrain
{
    public GridComponent grid;
    public GridTerrain[] cells;
    public int2[] spawns;
    public int attempt;
    public int rampCount;
    public int sealedIslandCount, sealedIslandCells;
    public string connectivityFailure;
    public int[] sourceVertexLevels; // Shared-vertex source, retained for diagnostics before terracing.
    public bool[] baseCore;
    public float spawnSectorOffset;
    public int baseHeightChanged, basePocketRepairedCells;
    public int componentPasses, rampCandidateLanes, rampQuotaShortfall;
    public TerrainGenerationProfile profile;
    internal int[] walkLabels, cliffLabels, floodQueue;
    public int[] rampFaceDirections = Array.Empty<int>(); // Exact low/high crossings; -1 means flat.
}

// Managed, readable generation first. No clearance, balance gate or sprite collision.
// Uses the existing C# RNG/permutation; same seed is not promised to match JavaScript.
public static class TerrainGeneration
{
    public static readonly int2[] Directions = GridTerrain.RampDirections;
    static float Noise(float2 p, float wavelength, uint[] permutation)
        => MapGenerator.fbm2D(p, 3, 2, 1 / wavelength, .5f, permutation) * 2 - 1;
    static int Next(int i, int d, GridComponent g)
        => TerrainTopologyOptimizer.Next(i,d,g);
    static bool Protected(int i, GeneratedTerrain m, float radius)
    {
        float2 p = (float2)GridHelper.GetGridPosFromIndex(i, m.grid) + .5f;
        foreach (var b in m.spawns) if (math.distance(p, (float2)b + .5f) <= radius) return true;
        return false;
    }
    public static GeneratedTerrain Generate(GridComponent g, TerrainGenerationSettings s, Action<int, string> reportAttempt = null)
    {
        ValidateSettings(g, s);
        return SharedVertexTerrainGeneration.Generate(g, s, reportAttempt);
    }
    // Finish shared-vertex terraces with cliff/ramp topology and gameplay validation.
    public static bool TryFinishTerraces(GeneratedTerrain m,TerrainGenerationSettings s,out string failure)
    {
        failure="";
        try {
            long stamp=TerrainGenerationProfile.Start(m);
            LimitHeightSteps(m);
            TerrainGenerationProfile.End(m,"heightLimit",ref stamp);
            TerrainTopologyOptimizer.ProtectBaseGeometry(m,s);
            TerrainGenerationProfile.End(m,"baseRecheck",ref stamp);
            TerrainTopologyOptimizer.Bake(m,s);
            TerrainGenerationProfile.End(m,"cliff",ref stamp);
            TerrainTopologyOptimizer.OpenRamps(m,s);
            TerrainGenerationProfile.End(m,"ramp",ref stamp);
            if(TerrainTopologyOptimizer.RepairProtectedPockets(m,s)){
                TerrainTopologyOptimizer.Bake(m,s);
                TerrainTopologyOptimizer.OpenRamps(m,s);
            }
            TerrainGenerationProfile.End(m,"protectedPocket",ref stamp);
            bool sealedMap=TrySealSmallIslands(m,s,out failure);
            TerrainGenerationProfile.End(m,"seal",ref stamp);
            if(!sealedMap)return false;
            Validate(m,s);TerrainGenerationProfile.End(m,"validation",ref stamp);return true;
        }catch(InvalidOperationException e){failure=e.Message;return false;}
    }
    static void ValidateSettings(GridComponent g, TerrainGenerationSettings s)
    {
        if (s == null || g.width < 16 || g.height < 16 || g.width > 512 || g.height > 512 ||
            !math.isfinite(g.cellsize) || g.cellsize <= 0 || s.players < 2 || s.players > 10 ||
            !math.isfinite(s.protectedRadius) || s.protectedRadius < 1 || !math.isfinite(s.wavelength) || s.wavelength <= 0 ||
            !math.isfinite(s.levelStep) || s.levelStep <= 0 || !math.isfinite(s.landscapeHeight) || s.landscapeHeight <= 0 ||
            !math.isfinite(s.detailAmplitude) || s.detailAmplitude < 0 || s.cliffCells < 1 || s.cliffCells > 3 || s.rampWidth < 1 || s.rampWidth > 12 || s.maximumRampCells < 1 || s.maximumRampCells > 4096 ||
            !math.isfinite(s.mountainWavelength) || s.mountainWavelength < 1 || !math.isfinite(s.mountainThreshold) || math.abs(s.mountainThreshold)>1 ||
            !math.isfinite(s.mountainDensity) || s.mountainDensity<0 || s.mountainDensity>2 || !math.isfinite(s.mountainSpawnBuffer) || s.mountainSpawnBuffer<0 ||
            s.mountainMinimumClusterCells<1 || s.mountainMinimumClusterCells>4096 || s.battlefieldRadius<2 || s.battlefieldRadius>64 ||
            !math.isfinite(s.smallIslandFillBudget) || s.smallIslandFillBudget<0 || s.smallIslandFillBudget>.1f)
            throw new ArgumentException("Invalid grid or terrain settings");
    }
    // Builds mountain topology for the shared-vertex height source.
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
    public static int LowerMask(GeneratedTerrain m, int i)
    {
        int mask = 0;
        for (int d = 0; d < 4; d++) { int j = Next(i, d, m.grid); if (j >= 0 && !m.cells[j].isMountain && m.cells[j].heightLevel < m.cells[i].heightLevel) mask |= 1 << d; }
        return mask;
    }
    public static int[] ComponentLabels(GeneratedTerrain m, out int count)
        => TerrainTopologyOptimizer.Labels(m,false,out count,true);
    // After legal portals have been exhausted, replace only bounded non-spawn pockets.
    // Check all limits before writing cells so rejection never leaves a partial fill.
    public static bool TrySealSmallIslands(GeneratedTerrain m, TerrainGenerationSettings s, out string failure)
    {
        failure = null;
        var labels = TerrainTopologyOptimizer.Labels(m, false, out int count); if (count == 1) return true;
        if (m.spawns == null || m.spawns.Length == 0) { failure = "No spawn anchor defines the main island"; return false; }
        int main = labels[GridHelper.GetNodeIndex(m.spawns[0], m.grid)];
        if (main == 0) { failure = "Main spawn is blocked"; return false; }
        var sizes = new int[count + 1]; foreach (int label in labels) sizes[label]++;
        foreach (int2 spawn in m.spawns) if (labels[GridHelper.GetNodeIndex(spawn, m.grid)] != main)
        { failure = "Player spawn islands remain disconnected; cannot fill a spawn island"; return false; }
        int fill = 0;
        for (int label = 1; label <= count; label++) if (label != main)
        {
            fill += sizes[label];
        }
        int budget = (int)math.floor(m.cells.Length * s.smallIslandFillBudget);
        if (fill + m.sealedIslandCells > budget)
        { failure = $"Small-island fill requires {fill + m.sealedIslandCells} cells (map budget {budget})"; return false; }
        for (int i = 0; i < labels.Length; i++) if (labels[i] != 0 && labels[i] != main && (m.baseCore != null ? m.baseCore[i] : Protected(i, m, s.protectedRadius + 2)))
        { failure = "Small island touches a protected spawn core"; return false; }
        for (int i = 0; i < labels.Length; i++) if (labels[i] != 0 && labels[i] != main)
        { m.cells[i].isMountain = true; m.cells[i].walkable = false; m.cells[i].isCliff = false; m.cells[i].RampId = -1; m.cells[i].RampDirection = -1; m.cells[i].RampInfluence = false; }
        m.sealedIslandCount += count - 1;
        m.sealedIslandCells += fill;
        UnifyMountainLevels(m);
        return true;
    }
    public static void Validate(GeneratedTerrain m, TerrainGenerationSettings s)
    {
        TerrainTopologyOptimizer.Labels(m,false,out int count);
        if(count!=1)throw new InvalidOperationException("Expected one walk island");
        foreach(var spawn in m.spawns){
            int root=spawn.y*m.grid.width+spawn.x,level=m.cells[root].heightLevel;
            if(!m.cells[root].walkable)throw new InvalidOperationException("Blocked spawn");
            int r=(int)math.floor(s.protectedRadius);
            for(int y=Math.Max(0,spawn.y-r);y<=Math.Min(m.grid.height-1,spawn.y+r);y++)for(int x=Math.Max(0,spawn.x-r);x<=Math.Min(m.grid.width-1,spawn.x+r);x++){
                if((x-spawn.x)*(x-spawn.x)+(y-spawn.y)*(y-spawn.y)>=(r+1)*(r+1))continue;
                var c=m.cells[y*m.grid.width+x];if(c.heightLevel!=level||c.isMountain||c.isCliff||!c.walkable)throw new InvalidOperationException("Spawn core is not protected");
            }
        }
        for(int i=0;i<m.cells.Length;i++){
            var c=m.cells[i];if(c.isMountain&&(c.walkable||c.RampId>=0||c.RampInfluence))throw new InvalidOperationException("Mountain carved / blocking ramp influence");
            if(c.isCliff&&c.walkable&&c.RampId<0)throw new InvalidOperationException("Unmarked cliff opening");
            if(c.RampDirection>=0){
                int back=Next(i,(c.RampDirection+2)%4,m.grid),front=Next(i,c.RampDirection,m.grid);
                if(back<0||front<0||m.cells[back].isMountain||m.cells[front].isMountain||c.heightLevel-m.cells[back].heightLevel!=1)throw new InvalidOperationException("Invalid ramp slope/influence");
            }
            if(!c.walkable)continue;
            for(int d=0;d<4;d++){
                int j=Next(i,d,m.grid);if(j<0||!m.cells[j].walkable||c.heightLevel==m.cells[j].heightLevel)continue;
                int high=c.heightLevel>m.cells[j].heightLevel?i:j,up=c.heightLevel<m.cells[j].heightLevel?d:(d+2)%4;
                if(Math.Abs(c.heightLevel-m.cells[j].heightLevel)!=1||m.cells[high].RampId<0||m.cells[high].RampDirection!=up)throw new InvalidOperationException("Illegal tier crossing");
            }
        }
    }
}

