using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

// Preview port of Lab5 cluster sampling. Uses actual baked prefab footprints.
public static class MapResourcePlanner
{
    public struct Node { public Entity Prefab; public float3 Position; public int2 Min, Max; public int Level, Role, PlayerSlot, ClusterId; public float2 ClusterCenter;
        public FixedString64Bytes DefinitionId; public ResourceValueTier Tier; public int AmountPerMine; }
    struct Cluster { public float2 Center; public float Radius; }

    public static List<Node> Plan(EntityManager em, GridComponent grid, NativeArray<GridTerrain> terrain,
        NativeArray<GridNodeCost> costs, NativeArray<bool> occupied, int2[] bases,
        MapResourceSettings config, DynamicBuffer<MapResourcePrefab> prefabs, out MapResourceReport report)
    {
        foreach (var entry in prefabs)
        {
            var e = entry.Value;
            if (!em.Exists(e) || !em.HasComponent<ResourceNodeData>(e) || !em.HasComponent<ResourceNodeTag>(e) ||
                !em.HasComponent<GridFootprint>(e) || !em.HasComponent<BlockageData>(e) || !em.HasComponent<Unity.Transforms.LocalTransform>(e) ||
                entry.AmountPerMine <= 0 || em.GetComponentData<BlockageData>(e).CustomCost != 255 ||
                em.GetComponentData<ResourceNodeData>(e).Type != entry.Type)
                throw new InvalidOperationException("Map resource requires a snapped resource prefab with positive configured stock and blocking footprint.");
        }
        for (int role = 0; role < 4; role++)
            if (config.Quotas[role] > 0 && Eligible(prefabs, role).Count == 0)
                throw new InvalidOperationException($"Resource catalog has no eligible prefab for cluster role {role}.");
        var rng = new Unity.Mathematics.Random(math.max(1u, config.Seed ^ 0x947a53e1u));
        var result = new List<Node>();
        var clusters = new List<Cluster>();
        report = default;
        var initial = new float[bases.Length][];
        for (int p = 0; p < bases.Length; p++) initial[p] = PathDistances(grid, costs, bases[p]);
        var secondaryMax = new float[bases.Length];
        Array.Fill(secondaryMax, -1);
        var originalCosts = costs.ToArray();
        // Reserve every player's guaranteed main package before optional clusters.
        for (int role = 0; role < 4; role++)
        for (int p = 0; p < bases.Length; p++)
        for (int c = 0; c < config.Quotas[role]; c++)
        {
            report.RequestedClusters++;
            int count = role == 0 ? config.MainMineCount : rng.NextInt(config.ClusterMin, config.ClusterMax + 1);
            var pool = Eligible(prefabs, role);
            var recipeRandom = role == 0 ? new Unity.Mathematics.Random(math.max(1u, config.Seed ^ 0x3517a91bu ^ (uint)c)) : rng;
            var recipe = new MapResourcePrefab[count];
            for (int i = 0; i < count; i++) recipe[i] = Choose(pool, ref recipeRandom);
            if (role != 0) rng = recipeRandom;
            float radius = math.max(config.MinimumClusterRadius, math.sqrt(count) * config.RadiusPerSqrtCount);
            bool accepted = false;
            for (int attempt = 0; attempt < config.CenterAttempts; attempt++)
            {
                float2 mapCenter = new float2(grid.width, grid.height) * .5f;
                float angle = role == 3 ? math.atan2(bases[p].y - mapCenter.y, bases[p].x - mapCenter.x) +
                    (rng.NextFloat() - .5f) * 2 * math.PI / bases.Length : rng.NextFloat() * 2 * math.PI;
                float2 range = grid.width * (role == 1 ? config.SecondaryRange : role == 2 ? config.AdvantageRange : config.ContestedRange);
                float distance = role == 0 ? config.MainDistanceCells : math.sqrt(math.lerp(range.x * range.x, range.y * range.y, rng.NextFloat()));
                float2 center = (role == 3 ? mapCenter : (float2)bases[p]) + new float2(math.cos(angle), math.sin(angle)) * distance;
                if (role != 0) center = math.round(center);
                if (math.any(center < radius + 3) || center.x >= grid.width - radius - 3 || center.y >= grid.height - radius - 3) continue;
                bool overlaps = false;
                foreach (var cluster in clusters)
                    if (math.distance((float2)center, cluster.Center) < radius + cluster.Radius + config.ClusterGap) { overlaps = true; break; }
                if (overlaps) continue;
                int ci = GridHelper.GetNodeIndex((int2)math.round(center), grid);
                var tile = terrain[ci];
                if (costs[ci].cost != 1 || tile.isMountain || tile.isCliff || tile.RampId >= 0) continue;
                var trial = new List<Node>();
                for (int draw = 0; draw < config.MemberAttempts && trial.Count < count; draw++)
                {
                    float a = rng.NextFloat() * 2 * math.PI, r = math.sqrt(rng.NextFloat()) * radius;
                    int2 cell = (int2)math.round(center + new float2(math.cos(a), math.sin(a)) * r);
                    var definition = recipe[trial.Count];
                    Entity prefab = definition.Value;
                    var footprint = em.GetComponentData<GridFootprint>(prefab);
                    float3 pos = GridSnapperMath.Snap(GridHelper.GridToWorld(cell, grid), grid, footprint.Cells);
                    if (!PlayerSpawnPlacement.TryFootprint(grid, pos, em.GetComponentData<BlockageData>(prefab).LocalRect, out var min, out var max)) continue;
                    // Keep ramp entrances/exits and mountain artwork clear, including diagonal neighbours.
                    // Check the entire footprint's one-cell margin; reserve only the mine footprint.
                    bool nearMountainOrRamp=false;
                    for(int y=math.max(0,min.y-1);y<=math.min(grid.height-1,max.y+1)&&!nearMountainOrRamp;y++)
                        for(int x=math.max(0,min.x-1);x<=math.min(grid.width-1,max.x+1);x++)
                            if(terrain[y*grid.width+x].isMountain || terrain[y*grid.width+x].RampId >= 0){nearMountainOrRamp=true;break;}
                    if(nearMountainOrRamp)continue;
                    bool valid = true;
                    foreach (var node in trial)
                        if (math.distance((float2)cell, GridHelper.WorldToGrid(node.Position, grid)) < config.MemberSpacing ||
                            math.all(min <= node.Max) && math.all(max >= node.Min)) { valid = false; break; }
                    foreach (var b in bases) if (math.distance((float2)cell, b) < 3) valid = false;
                    for (int y = min.y; y <= max.y && valid; y++) for (int x = min.x; x <= max.x; x++)
                    {
                        int i = GridHelper.GetNodeIndex(new int2(x, y), grid);
                        var t = terrain[i];
                        if (costs[i].cost != 1 || occupied[i] || !t.walkable || t.isMountain || t.isCliff || t.RampId >= 0 || t.heightLevel != tile.heightLevel)
                        { valid = false; break; }
                    }
                    if (valid) trial.Add(new Node { Prefab = prefab, Position = pos, Min = min, Max = max, Level = tile.heightLevel,
                        Role = role, PlayerSlot = p, ClusterId = clusters.Count, ClusterCenter = center,
                        DefinitionId = definition.Id, Tier = definition.Tier, AmountPerMine = definition.AmountPerMine });
                }
                if (trial.Count != count) continue;
                float sum = 0, nearest = float.PositiveInfinity, farthest = 0;
                foreach (var node in trial)
                {
                    float d = AccessDistance(grid, terrain, costs, node, initial[p]);
                    sum += d; nearest = math.min(nearest, d); farthest = math.max(farthest, d);
                }
                float avg = sum / count;
                if (role != 3 && (!math.isfinite(avg) ||
                    role == 1 && (avg < grid.width * .08f || avg > grid.width * .3f) ||
                    role == 2 && (avg > grid.width * .5f || nearest < (secondaryMax[p] >= 0 ? secondaryMax[p] + 4 : grid.width * .18f)))) continue;
                foreach (var node in trial) PlayerSpawnPlacement.Reserve(grid, costs, node.Min, node.Max, 255);
                var finalDistances = Distances(grid, costs, bases[0]);
                bool connected = true;
                for (int i = 0; i < costs.Length; i++) if (costs[i].cost < 255 && !math.isfinite(finalDistances[i])) { connected = false; break; }
                foreach (var b in bases) { int source = Source(grid, costs, b); if (source < 0 || !math.isfinite(finalDistances[source])) connected = false; }
                if (role == 3 && bases.Length < 2) connected = false;
                foreach (var node in result) if (!math.isfinite(AccessDistance(grid, terrain, costs, node, finalDistances))) connected = false;
                foreach (var node in trial) if (!math.isfinite(AccessDistance(grid, terrain, costs, node, finalDistances))) connected = false;
                if (!connected)
                {
                    foreach (var node in trial) PlayerSpawnPlacement.Reserve(grid, costs, node.Min, node.Max, 1);
                    continue;
                }
                if (role == 1) secondaryMax[p] = math.max(secondaryMax[p], farthest);
                result.AddRange(trial); clusters.Add(new Cluster { Center = center, Radius = radius });
                if (role == 0) { report.MainPlacedClusters++; report.MainNodes += count; }
                accepted = true;
                report.PlacedClusters++; break;
            }
            if (role == 0 && !accepted)
            {
                for (int i = 0; i < costs.Length; i++) costs[i] = originalCosts[i];
                throw new InvalidOperationException($"Cannot place balanced main cluster for player slot {p}: {count} mines at distance {config.MainDistanceCells} cells. No partial starting map will spawn.");
            }
        }
        report.Nodes = result.Count;
        return result;
    }

    // Select the highest eligible tier for this role; weight chooses a definition within that tier.
    static List<MapResourcePrefab> Eligible(DynamicBuffer<MapResourcePrefab> catalog, int role)
    {
        var result = new List<MapResourcePrefab>();
        int highest = -1;
        foreach (var entry in catalog)
        {
            int tier = (int)entry.Tier;
            if (((int)entry.AllowedClusters & (1 << role)) == 0 || tier == 1 && role < 2 || tier == 2 && role != 3) continue;
            if (tier < highest) continue;
            if (tier > highest) { result.Clear(); highest = tier; }
            result.Add(entry);
        }
        return result;
    }

    static MapResourcePrefab Choose(List<MapResourcePrefab> pool, ref Unity.Mathematics.Random rng)
    {
        long total = 0;
        foreach (var entry in pool) total += entry.Weight;
        if (total <= 0 || total > int.MaxValue) throw new InvalidOperationException("Invalid/overflowing resource selection weights.");
        int pick = rng.NextInt((int)total);
        foreach (var entry in pool) { if (pick < entry.Weight) return entry; pick -= entry.Weight; }
        throw new InvalidOperationException("Resource selection failed.");
    }

    static float AccessDistance(GridComponent grid, NativeArray<GridTerrain> terrain, NativeArray<GridNodeCost> costs, Node node, float[] distances)
    {
        float best = float.PositiveInfinity;
        for (int y = node.Min.y - 1; y <= node.Max.y + 1; y++) for (int x = node.Min.x - 1; x <= node.Max.x + 1; x++)
        {
            if (x >= node.Min.x && x <= node.Max.x && y >= node.Min.y && y <= node.Max.y) continue;
            var cell = new int2(x, y);
            if (!PlayerSpawnPlacement.InBounds(cell, grid)) continue;
            int i = GridHelper.GetNodeIndex(cell, grid);
            if (costs[i].cost == 1 && terrain[i].heightLevel == node.Level) best = math.min(best, distances[i]);
        }
        return best;
    }

    // Cardinal flood is sufficient for connectivity when diagonal corner cutting is forbidden.
    static float[] Distances(GridComponent grid, NativeArray<GridNodeCost> costs, int2 source)
    {
        var distances = new float[costs.Length]; Array.Fill(distances, float.PositiveInfinity);
        var queue = new Queue<int2>();
        int si = Source(grid, costs, source);
        if (si < 0) return distances;
        distances[si] = 0; queue.Enqueue(GridHelper.GetGridPosFromIndex(si, grid));
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue(); float d = distances[GridHelper.GetNodeIndex(cell, grid)] + 1;
            // Keep the flood independent of the terrain renderer's static direction table.
            for (int directionIndex = 0; directionIndex < 4; directionIndex++)
            {
                int2 direction = directionIndex == 0 ? new int2(0, -1)
                    : directionIndex == 1 ? new int2(1, 0)
                    : directionIndex == 2 ? new int2(0, 1) : new int2(-1, 0);
                int2 next = cell + direction;
                if (!PlayerSpawnPlacement.InBounds(next, grid)) continue;
                int i = GridHelper.GetNodeIndex(next, grid);
                if (costs[i].cost >= 255 || math.isfinite(distances[i])) continue;
                distances[i] = d; queue.Enqueue(next);
            }
        }
        return distances;
    }

    static int Source(GridComponent grid, NativeArray<GridNodeCost> costs, int2 source)
    {
        int best = -1; float distance = float.PositiveInfinity;
        for (int y = -4; y <= 4; y++) for (int x = -4; x <= 4; x++)
        {
            var cell = source + new int2(x, y);
            if (!PlayerSpawnPlacement.InBounds(cell, grid)) continue;
            int i = GridHelper.GetNodeIndex(cell, grid);
            if (costs[i].cost < 255 && x*x + y*y < distance) { best = i; distance = x*x + y*y; }
        }
        return best;
    }

    static float[] PathDistances(GridComponent grid, NativeArray<GridNodeCost> costs, int2 source)
    {
        var distances = new float[costs.Length]; Array.Fill(distances, float.PositiveInfinity);
        var heap = new List<KeyValuePair<float, int>>();
        int si = Source(grid, costs, source);
        if (si < 0) return distances;
        distances[si] = 0; Push(heap, new KeyValuePair<float, int>(0, si));
        while (heap.Count > 0)
        {
            var current = Pop(heap);
            if (current.Key != distances[current.Value]) continue;
            var cell = GridHelper.GetGridPosFromIndex(current.Value, grid);
            for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++)
            {
                if (x == 0 && y == 0) continue;
                var next = cell + new int2(x, y);
                if (!PlayerSpawnPlacement.InBounds(next, grid)) continue;
                int i = GridHelper.GetNodeIndex(next, grid);
                if (costs[i].cost >= 255) continue;
                if (x != 0 && y != 0 && (costs[GridHelper.GetNodeIndex(cell + new int2(x, 0), grid)].cost >= 255 ||
                    costs[GridHelper.GetNodeIndex(cell + new int2(0, y), grid)].cost >= 255)) continue;
                float d = current.Key + (x != 0 && y != 0 ? math.SQRT2 : 1);
                if (d >= distances[i]) continue;
                distances[i] = d; Push(heap, new KeyValuePair<float, int>(d, i));
            }
        }
        return distances;
    }

    static void Push(List<KeyValuePair<float, int>> heap, KeyValuePair<float, int> value)
    {
        int i = heap.Count; heap.Add(value);
        while (i > 0)
        {
            int parent = (i - 1) / 2;
            if (heap[parent].Key <= value.Key) break;
            heap[i] = heap[parent]; i = parent;
        }
        heap[i] = value;
    }

    static KeyValuePair<float, int> Pop(List<KeyValuePair<float, int>> heap)
    {
        var result = heap[0]; var last = heap[heap.Count - 1]; heap.RemoveAt(heap.Count - 1);
        if (heap.Count == 0) return result;
        int i = 0;
        while (i * 2 + 1 < heap.Count)
        {
            int child = i * 2 + 1;
            if (child + 1 < heap.Count && heap[child + 1].Key < heap[child].Key) child++;
            if (heap[child].Key >= last.Key) break;
            heap[i] = heap[child]; i = child;
        }
        heap[i] = last; return result;
    }
}

