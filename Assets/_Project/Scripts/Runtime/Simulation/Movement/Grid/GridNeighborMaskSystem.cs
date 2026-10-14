using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

[InternalBufferCapacity(0)]
public struct GridNeighborMask : IBufferElementData
{
    public byte Value;
}

public struct GridNeighborMaskState : IComponentData
{
    public uint TopologyVersion;
    public int Width;
    public int Height;
    public bool Built;
}

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(GridInitSystem))]
[UpdateAfter(typeof(CostChangeSystem))]
[UpdateAfter(typeof(GridIslandSystem))]
[UpdateBefore(typeof(IntegrationFieldSystem))]
public partial struct GridNeighborMaskSystem : ISystem
{
    private EntityQuery missingState;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridComponent>();
        missingState = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<GridComponent, GridNodeCost>()
            .WithNone<GridNeighborMaskState>()
            .Build(ref state);
    }

    public void OnUpdate(ref SystemState state)
    {
        // Also supports grids made by tests/runtime rather than GridAuthoring.
        if (!missingState.IsEmptyIgnoreFilter)
        {
            state.Dependency.Complete();
            using var grids = missingState.ToEntityArray(Allocator.Temp);
            foreach (var entity in grids)
            {
                if (!state.EntityManager.HasBuffer<GridNeighborMask>(entity))
                    state.EntityManager.AddBuffer<GridNeighborMask>(entity);
                state.EntityManager.AddComponentData(entity, new GridNeighborMaskState());
            }
        }

        foreach (var (gridRef, cacheRef, entity) in SystemAPI.Query<RefRO<GridComponent>, RefRW<GridNeighborMaskState>>().WithEntityAccess())
        {
            var grid = gridRef.ValueRO;
            if (!grid.initialized || grid.width <= 0 || grid.height <= 0) continue;
            int size = grid.width * grid.height;
            var cache = cacheRef.ValueRO;
            if (cache.Built && cache.TopologyVersion == grid.topologyVersion && cache.Width == grid.width && cache.Height == grid.height)
                continue;

            var costs = SystemAPI.GetBufferLookup<GridNodeCost>(true)[entity];
            if (costs.Length != size) continue;
            var masks = SystemAPI.GetBuffer<GridNeighborMask>(entity);
            masks.ResizeUninitialized(size);
            var job = new CalculateMasksJob
            {
                Width = grid.width,
                Height = grid.height,
                Costs = costs.AsNativeArray(),
                Masks = masks.AsNativeArray()
            };
            state.Dependency = job.Schedule(size, 128, state.Dependency);
            // Integration's buffer read depends on this system's scheduled write.
            cacheRef.ValueRW = new GridNeighborMaskState
            {
                Built = true, TopologyVersion = grid.topologyVersion,
                Width = grid.width, Height = grid.height
            };
        }
    }

    [BurstCompile]
    public struct CalculateMasksJob : IJobParallelFor
    {
        public int Width;
        public int Height;
        [ReadOnly] public NativeArray<GridNodeCost> Costs;
        public NativeArray<GridNeighborMask> Masks;

        public void Execute(int index)
        {
            Masks[index] = new GridNeighborMask { Value = GridNeighborMaskUtility.Calculate(index, Width, Height, Costs) };
        }
    }
}
