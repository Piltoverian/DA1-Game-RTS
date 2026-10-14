using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics.Systems;

public struct TerrainHeightData : IComponentData
{
    public GridComponent Grid;
    // x=00, y=10, z=11, w=01. Exact two-triangle surface, including legacy ramps.
    public NativeArray<float4> Corners;
    public bool Ready;

    public float Sample(float2 xz)
    {
        if (!Ready) return 0;
        float2 p = (xz - Grid.origin.xz) / Grid.cellsize;
        int2 cell = (int2)math.floor(p);
        if (math.any(cell < 0) || cell.x >= Grid.width || cell.y >= Grid.height) return 0;
        float2 f = p - cell;
        float4 h = Corners[cell.y * Grid.width + cell.x];
        return f.y <= f.x
            ? h.x + (h.y - h.x) * f.x + (h.z - h.y) * f.y
            : h.x + (h.z - h.w) * f.x + (h.w - h.x) * f.y;
    }
}

// Grid/revision owns cached geometry; renderer lifecycle is not a dependency.
[UpdateInGroup(typeof(InitializationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
[UpdateAfter(typeof(GridSnapperSystem))]
public partial class TerrainHeightCacheSystem : SystemBase
{
    Entity sourceGrid;
    uint sourceRevision;
    EntityQuery grids;
    Entity dataEntity;
    EntityQuery unregistered;
    protected override void OnCreate() {
        dataEntity = EntityManager.CreateEntity(typeof(TerrainHeightData));
        unregistered = TerrainHeightRegistration.CreateQuery(EntityManager);
        grids = GetEntityQuery(typeof(GridComponent), typeof(GridTerrain));
    }
    protected override void OnUpdate()
    {
        TerrainHeightRegistration.Register(EntityManager, unregistered);
        TerrainSurfaceData geometry = null;
        Entity gridEntity = Entity.Null; uint revision = 0;
        if (grids.CalculateEntityCount() == 1)
        {
            gridEntity = grids.GetSingletonEntity();
            bool ready = true;
            if (EntityManager.HasComponent<MapBuildState>(gridEntity))
            {
                var status = EntityManager.GetComponentData<MapBuildState>(gridEntity);
                revision = status.Revision;
                ready = status.Phase != MapBuildPhase.Requested && status.Phase != MapBuildPhase.Failed;
            }
            var grid = EntityManager.GetComponentData<GridComponent>(gridEntity);
            var cells = EntityManager.GetBuffer<GridTerrain>(gridEntity, true);
            ready &= grid.width > 0 && grid.height > 0 && grid.cellsize > 0 && cells.Length == (long)grid.width * grid.height;
            if (ready)
            {
                if (sourceGrid == gridEntity && sourceRevision == revision) return;
                var map = new GeneratedTerrain { grid = grid, cells = new GridTerrain[cells.Length] };
                for (int i = 0; i < cells.Length; i++) map.cells[i] = cells[i];
                TerrainRampCompiler.Compile(map);
                geometry = new TerrainSurfaceData(map, TerrainSurfaceData.CanonicalRise(grid.cellsize));
            }
            else gridEntity = Entity.Null;
        }
        if (gridEntity == Entity.Null && sourceGrid == Entity.Null) return;
        EntityManager.CompleteAllTrackedJobs(); // Only at surface lifecycle boundaries.
        var previous = EntityManager.GetComponentData<TerrainHeightData>(dataEntity);
        if (previous.Corners.IsCreated) previous.Corners.Dispose();
        var data = new TerrainHeightData { Ready = geometry != null };
        if (geometry != null) {
            data.Grid = geometry.Map.grid;
            data.Corners = new NativeArray<float4>(geometry.Map.cells.Length, Allocator.Persistent);
            for (int i = 0; i < data.Corners.Length; i++)
                data.Corners[i] = geometry.Corners(i);
        }
        EntityManager.SetComponentData(dataEntity, data);
        using var pending = EntityManager.CreateEntityQuery(new EntityQueryDesc {
            All = new[] { ComponentType.ReadOnly<PendingHeight>(), ComponentType.ReadOnly<TerrainHeightOffset>(), ComponentType.ReadOnly<LocalTransform>() },
            Options = EntityQueryOptions.IgnoreComponentEnabledState
        });
        EntityManager.SetComponentEnabled<PendingHeight>(pending, true);
        sourceGrid = gridEntity; sourceRevision = revision;
    }
    protected override void OnDestroy()
    {
        EntityManager.CompleteAllTrackedJobs();
        if (!EntityManager.Exists(dataEntity)) return;
        var data = EntityManager.GetComponentData<TerrainHeightData>(dataEntity);
        if (data.Corners.IsCreated) data.Corners.Dispose();
    }
}

[BurstCompile]
public partial struct ApplyTerrainHeightJob : IJobEntity
{
    [ReadOnly] public TerrainHeightData Data;
    public void Execute(ref LocalTransform transform, in TerrainHeightOffset offset, EnabledRefRW<PendingHeight> pending)
    {
        var position = transform.Position;
        position.y = Data.Sample(position.xz) + offset.Value;
        transform.Position = position;
        pending.ValueRW = false;
    }
}

// Consume movement dirtiness in the same fixed tick, before the physics world is built.
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation)]
[UpdateAfter(typeof(MovementAgentActuatorSystem))]
[UpdateAfter(typeof(SetupUnitMoverDefaultPositionSystem))]
[UpdateBefore(typeof(PhysicsSystemGroup))]
public partial struct FixedTerrainHeightSystem : ISystem
{
    public void OnCreate(ref SystemState state) => state.RequireForUpdate<TerrainHeightData>();
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var data = SystemAPI.GetSingleton<TerrainHeightData>();
        if (data.Ready) state.Dependency = new ApplyTerrainHeightJob { Data = data }.ScheduleParallel(state.Dependency);
    }
}

// Catch non-fixed spawns (production/building placement) before transform propagation.
[UpdateInGroup(typeof(SimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation)]
[UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(ProductionSystem))]
[UpdateAfter(typeof(BuildingPlacementSystem))]
[UpdateBefore(typeof(TransformSystemGroup))]
public partial class TerrainHeightSystem : SystemBase
{
    EntityQuery unregistered;
    protected override void OnCreate() {
        RequireForUpdate<TerrainHeightData>();
        unregistered = TerrainHeightRegistration.CreateQuery(EntityManager);
    }
    protected override void OnUpdate()
    {
        TerrainHeightRegistration.Register(EntityManager, unregistered);
        var data = SystemAPI.GetSingleton<TerrainHeightData>();
        if (data.Ready) Dependency = new ApplyTerrainHeightJob { Data = data }.ScheduleParallel(Dependency);
    }
}
