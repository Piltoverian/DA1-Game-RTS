using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
[UpdateAfter(typeof(GridInitSystem))]
[UpdateAfter(typeof(FlowDirectionSystem))]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
partial struct UnitMovementSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        GridComponent grid= SystemAPI.GetSingleton<GridComponent>();
        DynamicBuffer<GridNode> nbuffer= SystemAPI.GetSingletonBuffer<GridNode>();
        if (nbuffer.Length == 0) return;    
        foreach (var(transform,move) in SystemAPI.Query<RefRW<LocalTransform>,RefRO<UnitMovementComponent>>())
        {
            if (!move.ValueRO.hastarget)
                continue;
            float3 pos = transform.ValueRO.Position;
            int2 cell = GridHelper.WorldToGrid(pos, grid);
            int index= GridHelper.GetNodeIndex(cell,grid);
            float2 dir = nbuffer[index].direction;
            float3 velocity= new float3(dir.x,0,dir.y)*move.ValueRO.speed;
            pos += velocity*SystemAPI.Time.DeltaTime;
            transform.ValueRW.Position = pos;
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
