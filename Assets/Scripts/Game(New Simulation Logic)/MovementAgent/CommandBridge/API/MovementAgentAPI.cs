using Unity.Entities;
using Unity.Mathematics;
using Unity.DebugDisplay;
public enum TargetChangeResult
{
    Success,
    NoAgentComponent,
    InvalidTarget
}
public static class MovementAgentAPI
{
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
        var steeringComponent = entityManager.GetComponentData<MovementSteeringComponent>(agentEntity);
        FlowFieldHelper.ReleaseFieldFromMoveComponent(ref agentComponent, agentEntity, ecb, entityManager);
        agentComponent.hastarget = false;
        agentComponent.velocity= float3.zero;
        ecb.SetComponent(agentEntity, agentComponent);
        steeringComponent.isSettled = true;
        steeringComponent.stuckTime = 0;
        steeringComponent.minDistanceToTarget = float.MaxValue;
        ecb.SetComponent(agentEntity, steeringComponent);
    }

    public static void PauseAgent(EntityManager entityManager, Entity agentEntity, EntityCommandBuffer ecb)
    {
        if (!entityManager.HasComponent<MovementAgentComponent>(agentEntity))
        {
            return;
        }
        var agentComponent = entityManager.GetComponentData<MovementAgentComponent>(agentEntity);
        var steeringComponent = entityManager.GetComponentData<MovementSteeringComponent>(agentEntity);
        agentComponent.hastarget = false;
        agentComponent.velocity = float3.zero;
        agentComponent.preferredVelocity = float3.zero;
        ecb.SetComponent(agentEntity, agentComponent);

        steeringComponent.isSettled = true;
        steeringComponent.stuckTime = 0;
        steeringComponent.minDistanceToTarget = float.MaxValue;
        ecb.SetComponent(agentEntity, steeringComponent);
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