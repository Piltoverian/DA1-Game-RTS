using Unity.Entities;
using Unity.Mathematics;
using Unity.DebugDisplay;
using Mono.Cecil;
public enum TargetChangeResult
{
    Success,
    NoAgentComponent,
    InvalidTarget
}
public static class MovementAgentAPI
{
    public static TargetChangeResult SetTarget(EntityManager entityManager, Entity agentEntity, float3 worldTarget,GridComponent gridComponent)
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
        agent.useSlotTarget = false; 
        entityManager.SetComponentData(agentEntity, agent);

        if (entityManager.HasComponent<MovementSteeringComponent>(agentEntity))
        {
            var steering = entityManager.GetComponentData<MovementSteeringComponent>(agentEntity);
            steering.isSettled = false;
            steering.stuckTime = 0;
            steering.minDistanceToTarget = float.MaxValue;
            entityManager.SetComponentData(agentEntity, steering);
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
        FlowFieldHelper.ReleaseFieldFromMoveComponent(ref agentComponent, ecb, entityManager);
        agentComponent.hastarget = false;
        agentComponent.velocity= float3.zero;
        ecb.SetComponent(agentEntity, agentComponent);
        steeringComponent.isSettled = true;
        steeringComponent.stuckTime = 0;
        steeringComponent.minDistanceToTarget = float.MaxValue;
        ecb.SetComponent(agentEntity, steeringComponent);
    }

    public static void StopAgent(EntityManager entityManager, Entity agentEntity, EntityCommandBuffer ecb)
    {
        if (!entityManager.HasComponent<MovementAgentComponent>(agentEntity))
        {
            return;
        }
        var agentComponent = entityManager.GetComponentData<MovementAgentComponent>(agentEntity);
        var steeringComponent = entityManager.GetComponentData<MovementSteeringComponent>(agentEntity);
        agentComponent.hastarget = false;
        agentComponent.velocity= float3.zero;
        ecb.SetComponent(agentEntity, agentComponent);
        steeringComponent.isSettled = true;
        steeringComponent.stuckTime = 0;
        steeringComponent.minDistanceToTarget = float.MaxValue;
        ecb.SetComponent(agentEntity, steeringComponent);
    }
}