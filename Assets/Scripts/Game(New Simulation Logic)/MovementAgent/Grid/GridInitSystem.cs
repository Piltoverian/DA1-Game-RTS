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
            PhysicsCollider collider = state.EntityManager.GetComponentData<PhysicsCollider>(entity);

            var clone = collider.Value.Value.Clone();

            var filter = clone.Value.GetCollisionFilter();
            //filter.BelongsTo = PhysicsLayersDefine.Ground;
            clone.Value.SetCollisionFilter(filter);
            collider.Value = clone;
            state.EntityManager.SetComponentData(entity, collider);
            
            var cbuffer = costBuffer;
            var ibuffer = islandBuffer;
            int totalNodes = grid.ValueRO.width * grid.ValueRO.height;
            
            cbuffer.ResizeUninitialized(totalNodes);
            ibuffer.ResizeUninitialized(totalNodes);
            
            for (int i = 0; i < totalNodes; i++)
            {
                cbuffer[i] = new GridNodeCost { cost = 1 };
                ibuffer[i] = new GridIsland { islandID = 0 };
            }
        }
        state.Enabled = false;
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}