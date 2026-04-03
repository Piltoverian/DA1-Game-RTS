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
        foreach(var (grid,entity) in SystemAPI.Query<RefRW<GridComponent>>().WithEntityAccess())
        {
            var requestbuffer = SystemAPI.GetBuffer<CostChangeRequest>(entity);
            var costbuffer = SystemAPI.GetBuffer<GridNodeCost>(entity);
           
            if (requestbuffer.Length == 0) continue;
            foreach(var request in requestbuffer)
            {
                float3 worldMin = new float3(request.area.MinPoint.x, 0, request.area.MinPoint.y);
                float3 worldMax = new float3(request.area.MaxPoint.x, 0, request.area.MaxPoint.y);
                int2 gridMin = GridHelper.WorldToGrid(worldMin, grid.ValueRW);
                int2 gridMax = GridHelper.WorldToGrid(worldMax, grid.ValueRW);
                gridMin= new int2(math.clamp(gridMin.x,0,grid.ValueRW.width-1),math.clamp(gridMin.y,0,grid.ValueRW.height-1));
                gridMax= new int2(math.clamp(gridMax.x, 0, grid.ValueRW.width - 1), math.clamp(gridMax.y, 0, grid.ValueRW.height - 1));
                for (int x = gridMin.x; x <= gridMax.x; x++)
                {
                    for(int y=gridMin.y; y<=gridMax.y; y++)
                    {
                        int index = GridHelper.GetNodeIndex(new int2(x, y), grid.ValueRW);
                        GridNodeCost nodeCostNew = costbuffer[index];
                        nodeCostNew.cost = request.newCost;
                        costbuffer[index]=nodeCostNew;
                    }
                }
            }
            grid.ValueRW.isDirty = true;
            requestbuffer.Clear();
        }
        foreach(var grid in SystemAPI.Query<RefRW<GridComponent>>())
        {
            if (grid.ValueRO.isDirty)
                grid.ValueRW.HeartbeatTimer += 1;
            if (grid.ValueRW.isDirty && grid.ValueRW.HeartbeatTimer >= HEARTBEAT_INTERVAL)
            {
                grid.ValueRW.generation++;
                grid.ValueRW.HeartbeatTimer = 0;
                grid.ValueRW.isDirty = false;
            }
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
