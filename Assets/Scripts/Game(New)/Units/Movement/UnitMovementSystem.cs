using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEditor;

struct FieldChangeRequest:IComponentData
{
    public Entity Field;
}

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
        FixedFrameCount fixedFrameCount=SystemAPI.GetSingleton<FixedFrameCount>();
        Entity FieldCacheEnity = SystemAPI.GetSingletonEntity<FlowFieldCache>();
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
                   FlowFieldCacheHelper.GetOrCreateFlowField(ref state, FieldCacheEnity, ecb, worldTarget,(uint)fixedFrameCount.value,grid);

                
                move.ValueRW.hastarget = true;
                ecb.AddComponent(entity, new FieldChangeRequest
                {
                    Field = field,
                });
                continue;
            }
        }
        ecb.Playback(state.EntityManager);
        var ecb2= new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        foreach(var (fieldChangeRequest,move,entity) in SystemAPI.Query<RefRO<FieldChangeRequest>,RefRW<UnitMovementComponent>>().WithEntityAccess()){
            PathFindingHelper.AssignFieldToMoveComponent(ref move.ValueRW, fieldChangeRequest.ValueRO.Field, ref state);
            ecb2.RemoveComponent<FieldChangeRequest>(entity);
        }
        ecb2.Playback(state.EntityManager);
        foreach (var (transform, move, entity) in
            SystemAPI.Query<RefRW<LocalTransform>, RefRW<UnitMovementComponent>>()
            .WithEntityAccess())
        {

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
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {

    }
}