using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
public enum TargetChangeResult
{
    Success,
    NoAgentComponent,
    InvalidTarget
}
public static class MovementAgentAPI
{
    // Keep field ownership intact; ClearTarget releases it separately.
    public static void StopAgent(ref MovementAgentComponent agent, MovementStopReason reason)
    {
        agent.hastarget = false;
        agent.velocity = float3.zero;
        agent.preferredVelocity = float3.zero;
        agent.stopReason = reason;
    }

    public static void StopAgent(ref MovementAgentComponent agent, ref MovementSteeringComponent steering, MovementStopReason reason)
    {
        StopAgent(ref agent, reason);
        steering.isSettled = true;
        steering.stuckTime = 0;
        steering.minDistanceToTarget = float.MaxValue;
    }

    public static TargetChangeResult SetTarget(EntityManager entityManager, Entity agentEntity, float3 worldTarget, GridComponent gridComponent, EntityCommandBuffer ecb)
    {
        if (!entityManager.HasComponent<MovementAgentComponent>(agentEntity))
        {
            return TargetChangeResult.NoAgentComponent;
        }
        var targetcell = GridHelper.WorldToGrid(worldTarget, gridComponent);
        if (targetcell.x >= gridComponent.width || targetcell.x < 0 || targetcell.y >= gridComponent.height || targetcell.y < 0)
        {
            return TargetChangeResult.InvalidTarget;
        }
        var agent= entityManager.GetComponentData<MovementAgentComponent>(agentEntity);
        agent.currentworldtarget = worldTarget;
        agent.hastarget = true;
        agent.stopReason = MovementStopReason.None;
        // The old navigation cell/field cannot represent a newly issued target.
        // This also lets PathRequestSystem recover if a request was lost.
        agent.targetResolutionKind = TargetResolutionKind.None;
        ecb.SetComponent(agentEntity, agent);

        // Luôn trigger TargetChangeRequest khi có lệnh đổi đích, thay vì phụ thuộc vào PathRequestSystem (vốn bỏ qua các lệnh di chuyển trong cùng 1 ô lưới)
        ecb.AddComponent(agentEntity, new TargetChangeRequest { newWorldTarget = worldTarget });

        if (entityManager.HasComponent<MovementSteeringComponent>(agentEntity))
        {
            var steering = entityManager.GetComponentData<MovementSteeringComponent>(agentEntity);
            steering.isSettled = false;
            steering.stuckTime = 0;
            steering.minDistanceToTarget = float.MaxValue;
            ecb.SetComponent(agentEntity, steering);
        }

        return TargetChangeResult.Success;
    }

    public static void ClearTarget(EntityManager entityManager, Entity agentEntity,EntityCommandBuffer ecb)
    {
        if (!entityManager.HasComponent<MovementAgentComponent>(agentEntity))
        {
            return;
        }
        var agentComponent = entityManager.GetComponentData<MovementAgentComponent>(agentEntity);
        FlowFieldHelper.ReleaseFieldFromMoveComponent(ref agentComponent, agentEntity, ecb, entityManager);
        if (entityManager.HasComponent<MovementSteeringComponent>(agentEntity))
        {
            var steering = entityManager.GetComponentData<MovementSteeringComponent>(agentEntity);
            StopAgent(ref agentComponent, ref steering, MovementStopReason.Released);
            ecb.SetComponent(agentEntity, steering);
        }
        else StopAgent(ref agentComponent, MovementStopReason.Released);
        ecb.SetComponent(agentEntity, agentComponent);
    }

    public static void PauseAgent(EntityManager entityManager, Entity agentEntity, EntityCommandBuffer ecb)
    {
        if (!entityManager.HasComponent<MovementAgentComponent>(agentEntity))
        {
            return;
        }
        var agentComponent = entityManager.GetComponentData<MovementAgentComponent>(agentEntity);
        if (entityManager.HasComponent<MovementSteeringComponent>(agentEntity))
        {
            var steering = entityManager.GetComponentData<MovementSteeringComponent>(agentEntity);
            StopAgent(ref agentComponent, ref steering, MovementStopReason.Paused);
            ecb.SetComponent(agentEntity, steering);
        }
        else StopAgent(ref agentComponent, MovementStopReason.Paused);
        ecb.SetComponent(agentEntity, agentComponent);
    }

    public static void PauseAgent(EntityManager entityManager, Entity agentEntity)
    {
        using var ecb = new EntityCommandBuffer(Allocator.Temp);
        PauseAgent(entityManager, agentEntity, ecb);
        ecb.Playback(entityManager);
    }

    public static void ResumeAgent(EntityManager entityManager, Entity agentEntity, EntityCommandBuffer ecb)
    {
        if (!entityManager.HasComponent<MovementAgentComponent>(agentEntity))
        {
            return;
        }
        var agentComponent = entityManager.GetComponentData<MovementAgentComponent>(agentEntity);
        
        if (math.lengthsq(agentComponent.currentworldtarget) < 0.001f)
        {
            return;
        }

        agentComponent.hastarget = true;
        agentComponent.stopReason = MovementStopReason.None;
        ecb.SetComponent(agentEntity, agentComponent);

        if (entityManager.HasComponent<MovementSteeringComponent>(agentEntity))
        {
            var steeringComponent = entityManager.GetComponentData<MovementSteeringComponent>(agentEntity);
            steeringComponent.isSettled = false;
            steeringComponent.stuckTime = 0;
            steeringComponent.minDistanceToTarget = float.MaxValue;
            ecb.SetComponent(agentEntity, steeringComponent);
        }
    }
}
