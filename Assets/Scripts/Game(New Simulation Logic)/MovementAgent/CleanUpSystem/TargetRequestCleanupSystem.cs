using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Cleanup system: Xóa TargetChangeRequest sau khi TẤT CẢ systems trong
/// FixedStepSimulationSystemGroup đã xử lý xong.
/// 
/// Chạy cuối FixedStepSimulationSystemGroup để đảm bảo:
///   - FlowFieldAssignmentSystem đã gán FlowField
///   - GroupFormationSystem đã gán slot
///   - Mọi system khác cần đọc request đều đã hoàn tất
/// </summary>
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(FlowFieldAssignmentSystem))]
[UpdateAfter(typeof(MovementAgentActuatorSystem))]
public partial struct TargetRequestCleanupSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (request, entity) in SystemAPI.Query<RefRO<TargetChangeRequest>>()
            .WithEntityAccess())
        {
            ecb.RemoveComponent<TargetChangeRequest>(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
