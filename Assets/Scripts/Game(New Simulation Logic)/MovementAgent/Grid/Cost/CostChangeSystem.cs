using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Mathematics.Geometry;
using UnityEngine;


[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(GridInitSystem))]
[UpdateBefore(typeof(GridIslandSystem))]

partial struct CostChangeSystem : ISystem
{
    public const int HEARTBEAT_INTERVAL = 12;
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach(var (grid,entity) in SystemAPI.Query<RefRO<GridComponent>>().WithEntityAccess())
        {
            var requestbuffer = SystemAPI.GetBuffer<CostChangeRequest>(entity);
            if (requestbuffer.Length == 0) continue;
            bool changed = false;
            var costbuffer = SystemAPI.GetBuffer<GridNodeCost>(entity);
            bool hasTerrain = state.EntityManager.HasBuffer<GridTerrain>(entity);
            var terrain = hasTerrain ? state.EntityManager.GetBuffer<GridTerrain>(entity) : default;
            bool useTerrain = hasTerrain && terrain.Length == costbuffer.Length;

            foreach(var request in requestbuffer)
            {
                float3 worldMin = new float3(request.area.MinPoint.x + 0.01f, 0, request.area.MinPoint.y + 0.01f);
                float3 worldMax = new float3(request.area.MaxPoint.x - 0.01f, 0, request.area.MaxPoint.y - 0.01f);
                int2 gMin = GridHelper.WorldToGrid(worldMin, grid.ValueRO);
                int2 gMax = GridHelper.WorldToGrid(worldMax, grid.ValueRO);
                int2 gridMin = math.min(gMin, gMax);
                int2 gridMax = math.max(gMin, gMax);
                if (gridMax.x < 0 || gridMax.y < 0 || gridMin.x >= grid.ValueRO.width || gridMin.y >= grid.ValueRO.height) continue;
                gridMin = new int2(math.clamp(gridMin.x, 0, grid.ValueRO.width - 1), math.clamp(gridMin.y, 0, grid.ValueRO.height - 1));
                gridMax = new int2(math.clamp(gridMax.x, 0, grid.ValueRO.width - 1), math.clamp(gridMax.y, 0, grid.ValueRO.height - 1));
                for (int x = gridMin.x; x <= gridMax.x; x++)
                {
                    for (int y = gridMin.y; y <= gridMax.y; y++)
                    {
                        int index = GridHelper.GetNodeIndex(new int2(x, y), grid.ValueRO);
                        GridNodeCost nodeCostNew = costbuffer[index];
                        // A dynamic blocker removal can never open a static cliff/mountain.
                        int desired = useTerrain && !terrain[index].walkable ? 255 : request.newCost;
                        if (nodeCostNew.cost == desired) continue;
                        nodeCostNew.cost = desired;
                        changed = true;
                        costbuffer[index] = nodeCostNew;
                    }
                }
            }
            if (changed) {
                var updated = grid.ValueRO; updated.isDirty = true;
                SystemAPI.SetComponent(entity, updated);
            }
            requestbuffer.Clear();
        }
        foreach(var (grid, entity) in SystemAPI.Query<RefRO<GridComponent>>().WithEntityAccess())
        {
            if (!grid.ValueRO.isDirty) continue;
            var updated = grid.ValueRO;
            updated.HeartbeatTimer++;
            if (updated.HeartbeatTimer >= HEARTBEAT_INTERVAL)
            {
                updated.generation++;
                updated.HeartbeatTimer = 0;
                updated.isDirty = false;
            }
            SystemAPI.SetComponent(entity, updated);
        }
    }
    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
