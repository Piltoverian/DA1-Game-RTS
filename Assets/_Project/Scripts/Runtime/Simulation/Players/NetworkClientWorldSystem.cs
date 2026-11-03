using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

// Client-side interaction data comes only from the authorized terrain receiver.
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
[UpdateAfter(typeof(TerrainNetworkReceiveSystem))]
[UpdateBefore(typeof(MapTerrainPresentationSystem))]
public partial class NetworkClientWorldSystem : SystemBase
{
    Entity ownedFog;
    protected override void OnUpdate()
    {
        var receiver = World.GetExistingSystemManaged<TerrainNetworkReceiveSystem>();
        if (receiver == null || !receiver.View.Ready || !PlayerNetwork.TryLocal(World, out _, out var player, false))
        {
            if (ownedFog != Entity.Null && EntityManager.Exists(ownedFog))
            {
                EntityManager.CompleteAllTrackedJobs();
                var old = EntityManager.GetComponentData<FogOfWarSpatialSystemData>(ownedFog);
                if (old.Visible.IsCreated) old.Visible.Clear();
                if (old.Explored.IsCreated) old.Explored.Clear();
                old.Ready = false; EntityManager.SetComponentData(ownedFog, old);
            }
            return;
        }
        var grid = receiver.View.Grid; grid.initialized = true;
        using var grids = EntityManager.CreateEntityQuery(typeof(GridComponent));
        if (grids.CalculateEntityCount() > 1) return;
        Entity ge;
        if (grids.IsEmptyIgnoreFilter) ge = EntityManager.CreateEntity(typeof(GridComponent));
        else ge = grids.GetSingletonEntity();
        EntityManager.SetComponentData(ge, grid);
        using var fogs = EntityManager.CreateEntityQuery(typeof(FogOfWarSpatialSystemData));
        if (fogs.IsEmptyIgnoreFilter)
        {
            ownedFog = EntityManager.CreateEntity(typeof(FogOfWarSpatialSystemData));
            EntityManager.SetComponentData(ownedFog, new FogOfWarSpatialSystemData
            {
                Visible = new NativeParallelMultiHashMap<int, int>(1024, Allocator.Persistent),
                Explored = new NativeParallelMultiHashMap<int, int>(1024, Allocator.Persistent)
            });
        }
        if (fogs.CalculateEntityCount() != 1) return;
        var fog = fogs.GetSingleton<FogOfWarSpatialSystemData>();
        if (!fog.Visible.IsCreated || !fog.Explored.IsCreated) return;
        EntityManager.CompleteAllTrackedJobs();
        fog.Visible.Clear(); fog.Explored.Clear();
        foreach (var cell in receiver.View.Cells)
        {
            fog.Explored.Add(cell.Key, player.PlayerId);
            if (receiver.View.IsCellVisible(cell.Key)) fog.Visible.Add(cell.Key, player.PlayerId);
        }
        fog.SourceGrid = ge; fog.GridRevision = receiver.Header.Revision; fog.Ready = true;
        EntityManager.SetComponentData(fogs.GetSingletonEntity(), fog);
    }
    protected override void OnDestroy()
    {
        if (ownedFog == Entity.Null || !EntityManager.Exists(ownedFog)) return;
        EntityManager.CompleteAllTrackedJobs();
        var fog = EntityManager.GetComponentData<FogOfWarSpatialSystemData>(ownedFog);
        if (fog.Visible.IsCreated) fog.Visible.Dispose();
        if (fog.Explored.IsCreated) fog.Explored.Dispose();
    }
}
