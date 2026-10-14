using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using static UnityEngine.Rendering.ProbeAdjustmentVolume;
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
partial struct GridInitSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {

        foreach (var (grid, costBuffer, islandBuffer, entity) in SystemAPI.Query<RefRW<GridComponent>, DynamicBuffer<GridNodeCost>, DynamicBuffer<GridIsland>>().WithEntityAccess())
        {
            if(grid.ValueRO.initialized)continue;
            if(state.EntityManager.HasComponent<MapBuildState>(entity)){
                var phase=state.EntityManager.GetComponentData<MapBuildState>(entity).Phase;
                if(phase==MapBuildPhase.Requested||phase==MapBuildPhase.Failed)continue;
            }
            int totalNodes = grid.ValueRO.width * grid.ValueRO.height;
            
            var cbuffer = costBuffer;
            var ibuffer = islandBuffer;
            
            cbuffer.ResizeUninitialized(totalNodes);
            ibuffer.ResizeUninitialized(totalNodes);
            
            bool hasTerrain = state.EntityManager.HasBuffer<GridTerrain>(entity);
            var terrain = hasTerrain ? state.EntityManager.GetBuffer<GridTerrain>(entity) : default;
            bool useTerrain = hasTerrain && terrain.Length == totalNodes;
            for (int i = 0; i < totalNodes; i++)
            {
                cbuffer[i] = new GridNodeCost { cost = useTerrain && !terrain[i].walkable ? 255 : 1 };
                ibuffer[i] = new GridIsland { islandID = 0 };
            }
            grid.ValueRW.islandGeneration = uint.MaxValue;
            grid.ValueRW.topologyVersion++;
            grid.ValueRW.initialized = true;
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
