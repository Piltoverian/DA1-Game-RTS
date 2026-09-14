using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hệ thống tự động nạp và dọn dẹp Grid Cost cho tất cả các Blockage trong MovementAgent.
/// Chạy trước CostChangeSystem để đẩy các yêu cầu đổi Cost vào buffer.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TransformSystemGroup))]
public partial struct BlockageGridBakeSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonEntity<GridComponent>(out Entity gridEntity))
            return;

        if (!SystemAPI.HasBuffer<CostChangeRequest>(gridEntity))
            return;

        var costChangeBuffer = SystemAPI.GetBuffer<CostChangeRequest>(gridEntity);
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // 1. TỰ ĐỘNG NẠP COST KHI CÓ BLOCKAGE MỚI XUẤT HIỆN
        foreach (var (blockage, transform, entity) in 
                 SystemAPI.Query<RefRO<BlockageData>, RefRO<LocalTransform>>()
                     .WithAll<BlockageNeedBakeTag>()
                     .WithNone<Prefab>()
                     .WithEntityAccess())
        {
            float3 bPos = transform.ValueRO.Position;
            float2 worldMin = new float2(bPos.x + blockage.ValueRO.LocalRect.MinPoint.x, bPos.z + blockage.ValueRO.LocalRect.MinPoint.y);
            float2 worldMax = new float2(bPos.x + blockage.ValueRO.LocalRect.MaxPoint.x, bPos.z + blockage.ValueRO.LocalRect.MaxPoint.y);

            StartEndRect area = new StartEndRect(worldMin);
            area.ExpandTo(worldMax);

            costChangeBuffer.Add(new CostChangeRequest
            {
                newCost = blockage.ValueRO.CustomCost,
                area = area
            });
            ecb.AddComponent(entity, new BlockageCleanupData
            {
                Position = bPos,
                LocalRect = blockage.ValueRO.LocalRect
            });
            ecb.RemoveComponent<BlockageNeedBakeTag>(entity);
        }

        // 2. TỰ ĐỘNG HOÀN TRẢ COST KHI BLOCKAGE BỊ XÓA (CLEANUP)
        foreach (var (cleanup, entity) in 
                 SystemAPI.Query<RefRO<BlockageCleanupData>>()
                     .WithNone<BlockageData>()
                     .WithEntityAccess())
        {
            float3 bPos = cleanup.ValueRO.Position;
            float2 worldMin = new float2(bPos.x + cleanup.ValueRO.LocalRect.MinPoint.x, bPos.z + cleanup.ValueRO.LocalRect.MinPoint.y);
            float2 worldMax = new float2(bPos.x + cleanup.ValueRO.LocalRect.MaxPoint.x, bPos.z + cleanup.ValueRO.LocalRect.MaxPoint.y);

            StartEndRect area = new StartEndRect(worldMin);
            area.ExpandTo(worldMax);

            costChangeBuffer.Add(new CostChangeRequest
            {
                newCost = 1,
                area = area
            });

            // Xóa sạch Cleanup component để hoàn tất việc giải phóng Entity
            ecb.RemoveComponent<BlockageCleanupData>(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
