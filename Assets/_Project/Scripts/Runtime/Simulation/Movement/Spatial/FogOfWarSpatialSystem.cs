using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct FogOfWarSpatialSystemData : IComponentData
{
    public NativeParallelMultiHashMap<int, int> Visible;
    public NativeParallelMultiHashMap<int, int> Explored;

    public bool IsVisibleTo(int cellIndex, int playerId) => HasPlayer(Visible, cellIndex, playerId);
    public bool IsExploredBy(int cellIndex, int playerId) => HasPlayer(Explored, cellIndex, playerId);

    private static bool HasPlayer(NativeParallelMultiHashMap<int, int> map, int cellIndex, int playerId)
    {
        if (!map.IsCreated || playerId < 0 || !map.TryGetFirstValue(cellIndex, out int id, out var iterator)) return false;
        do { if (id == playerId) return true; } while (map.TryGetNextValue(out id, ref iterator));
        return false;
    }

    public static void AddUnique(ref NativeParallelMultiHashMap<int, int> map, int cellIndex, int playerId)
    {
        if (!HasPlayer(map, cellIndex, playerId)) map.Add(cellIndex, playerId);
    }

    public void ImportTeammateMap(in PlayerContext receiver, NativeArray<PlayerContext> players)
    {
        if (receiver.PlayerId < 0 || receiver.TeamID < 0 || receiver.FogMode != FogMode.Team) return;
        CopyTeammateCells(ref Visible, receiver, players);
        CopyTeammateCells(ref Explored, receiver, players);
    }

    private static void CopyTeammateCells(ref NativeParallelMultiHashMap<int, int> map,
        in PlayerContext receiver, NativeArray<PlayerContext> players)
    {
        if (!map.IsCreated) return;
        // Snapshot before adding: inserting may resize the map and invalidate its iterator.
        using var cells = map.GetKeyValueArrays(Allocator.Temp);
        for (int i = 0; i < cells.Keys.Length; i++)
        {
            int donorId = cells.Values[i];
            if (donorId == receiver.PlayerId) continue;
            foreach (var donor in players)
                if (donor.PlayerId == donorId && donor.TeamID == receiver.TeamID)
                {
                    AddUnique(ref map, cells.Keys[i], receiver.PlayerId);
                    break;
                }
        }
    }

}

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(UnitSpatialSystem))]
partial struct FogOfWarSpatialSystem : ISystem
{
    private Entity sourceGrid;
    private GridComponent previousGrid;
    private uint sourceRevision;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        if(!SystemAPI.HasSingleton<FogOfWarSpatialSystemData>())
        {
            var bucket = new FogOfWarSpatialSystemData
            {
                Visible = new NativeParallelMultiHashMap<int, int>(10000, Allocator.Persistent),
                Explored = new NativeParallelMultiHashMap<int, int>(10000, Allocator.Persistent)
            };
            state.EntityManager.CreateSingleton(bucket);
        }

        state.RequireForUpdate<GridComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var container = SystemAPI.GetSingletonRW<FogOfWarSpatialSystemData>();
        ref var data = ref container.ValueRW;
        data.Visible.Clear();

        var gridEntity = SystemAPI.GetSingletonEntity<GridComponent>();
        var grid = SystemAPI.GetSingleton<GridComponent>();
        uint revision = SystemAPI.HasComponent<MapBuildState>(gridEntity)
            ? SystemAPI.GetComponent<MapBuildState>(gridEntity).Revision : 0;
        if (sourceGrid != gridEntity || sourceRevision != revision
            || previousGrid.width != grid.width || previousGrid.height != grid.height
            || previousGrid.cellsize != grid.cellsize || math.any(previousGrid.origin != grid.origin))
        {
            data.Explored.Clear();
            sourceGrid = gridEntity;
            sourceRevision = revision;
            previousGrid = grid;
        }

        if (!grid.initialized || grid.width <= 0 || grid.height <= 0
            || (long)grid.width * grid.height > int.MaxValue
            || !math.isfinite(grid.cellsize) || grid.cellsize <= 0
            || !math.all(math.isfinite(grid.origin))) return;
        if (SystemAPI.HasComponent<MapBuildState>(gridEntity))
        {
            var phase = SystemAPI.GetComponent<MapBuildState>(gridEntity).Phase;
            if (phase == MapBuildPhase.Requested || phase == MapBuildPhase.Failed) return;
        }

        using var players = SystemAPI.QueryBuilder().WithAll<PlayerContext>().Build()
            .ToComponentDataArray<PlayerContext>(Allocator.Temp);
        var health = SystemAPI.GetComponentLookup<EntityHealth>(true);
        foreach (var (transform, owner, vision, entity) in
            SystemAPI.Query<RefRO<LocalTransform>, RefRO<EntityOwner>, RefRO<EntityVision>>()
                .WithNone<Bullet, ArtilleryBullet>().WithEntityAccess())
        {
            int playerId = owner.ValueRO.PlayerID;
            int radius = vision.ValueRO.VisionRadiusCells;
            if (playerId < 0 || radius < 0 || !math.all(math.isfinite(transform.ValueRO.Position))
                || (health.HasComponent(entity) && health[entity].CurrentHP <= 0)) continue;

            int2 center = GridHelper.WorldToGrid(transform.ValueRO.Position, grid);
            if (center.x < 0 || center.y < 0 || center.x >= grid.width || center.y >= grid.height) continue;
            int minX = center.x - math.min(radius, center.x);
            int minZ = center.y - math.min(radius, center.y);
            int maxX = center.x + math.min(radius, grid.width - 1 - center.x);
            int maxZ = center.y + math.min(radius, grid.height - 1 - center.y);
            int teamId = -1;
            foreach (var player in players)
                if (player.PlayerId == playerId) { teamId = player.TeamID; break; }
            long radiusSq = (long)radius * radius;
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                long dx = (long)x - center.x;
                long dz = (long)z - center.y;
                if (dx * dx + dz * dz > radiusSq) continue;
                int index = GridHelper.GetNodeIndex(new int2(x, z), grid);
                FogOfWarSpatialSystemData.AddUnique(ref data.Visible, index, playerId);
                FogOfWarSpatialSystemData.AddUnique(ref data.Explored, index, playerId);
                if (teamId < 0) continue;
                foreach (var receiver in players)
                {
                    if (receiver.PlayerId < 0 || receiver.PlayerId == playerId
                        || receiver.TeamID != teamId || receiver.FogMode != FogMode.Team) continue;
                    FogOfWarSpatialSystemData.AddUnique(ref data.Visible, index, receiver.PlayerId);
                    FogOfWarSpatialSystemData.AddUnique(ref data.Explored, index, receiver.PlayerId);
                }
            }
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (SystemAPI.HasSingleton<FogOfWarSpatialSystemData>())
        {
            var container = SystemAPI.GetSingleton<FogOfWarSpatialSystemData>();
            if (container.Visible.IsCreated)
                container.Visible.Dispose();
            if (container.Explored.IsCreated)
                container.Explored.Dispose();
        }
    }
}
