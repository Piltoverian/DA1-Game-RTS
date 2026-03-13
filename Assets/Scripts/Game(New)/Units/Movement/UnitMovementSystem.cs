using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

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

        GridComponent grid = SystemAPI.GetSingleton<GridComponent>();
        EntityCommandBuffer ecb= new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        foreach (var (transform, move, entity) in
            SystemAPI.Query<RefRW<LocalTransform>, RefRW<UnitMovementComponent>>()
            .WithEntityAccess())
        {
            // random target lần đầu
            if (move.ValueRO.FieldEntity == Entity.Null)
            {
                Unity.Mathematics.Random rand =
                    new Unity.Mathematics.Random(
                        (uint)(entity.Index + 1) * 12345);

                int x = rand.NextInt(0, grid.width);
                int y = rand.NextInt(0, grid.height);

                float3 worldTarget =
                    grid.origin +
                    new float3(
                        x * grid.cellsize,
                        0,
                        y * grid.cellsize);

                Entity field =
                    PathFindingHelper.FlowerFieldInit(
                        state.EntityManager,
                        worldTarget,
                        grid,ecb);

                move.ValueRW.FieldEntity = field;
                move.ValueRW.hastarget = true;

                continue;
            }

            Entity fieldEntity = move.ValueRO.FieldEntity;

            if (!state.EntityManager.Exists(fieldEntity))
                continue;

            DynamicBuffer<FieldNode> buffer =
                state.EntityManager.GetBuffer<FieldNode>(fieldEntity);

            float3 pos = transform.ValueRO.Position;

            int2 cell = GridHelper.WorldToGrid(pos, grid);
            int index = GridHelper.GetNodeIndex(cell, grid);

            if (index < 0 || index >= buffer.Length)
                continue;

            float2 dir = buffer[index].direction;

            float3 velocity =
                new float3(dir.x, 0, dir.y) * move.ValueRO.speed;

            pos += velocity * SystemAPI.Time.DeltaTime;

            transform.ValueRW.Position = pos;
        }
        ecb.Playback(state.EntityManager);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {

    }
}