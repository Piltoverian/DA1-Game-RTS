using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(GridInitSystem))]
[UpdateAfter(typeof(CostChangeSystem))]
[UpdateBefore(typeof(MovementAgentPathRequestSystem))]
[UpdateBefore(typeof(FlowFieldAssignmentSystem))]
[BurstCompile]
partial struct SetupUnitMoverDefaultPositionSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var gridComponent = SystemAPI.GetSingleton<GridComponent>();
        var gridEntity = SystemAPI.GetSingletonEntity<GridComponent>();
        var costs = SystemAPI.GetBuffer<GridNodeCost>(gridEntity).AsNativeArray();
        if (costs.Length != gridComponent.width * gridComponent.height) return;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        var em = state.EntityManager;

        foreach (var (transform, agent, entity)
                 in SystemAPI.Query<RefRW<LocalTransform>, RefRW<MovementAgentComponent>>()
                     .WithAll<SetupUnitMoverDefaultPosition>()
                     .WithEntityAccess())
        {
            float3 position = transform.ValueRO.Position;
            int2 cell = GridHelper.WorldToGrid(position, gridComponent);
            if (!NaturalBlockedTargetResolver.IsCellInBounds(cell, gridComponent)) continue;
            if (!NaturalBlockedTargetResolver.IsCellWalkable(cell, gridComponent, costs))
            {
                if (!NaturalBlockedTargetResolver.TryResolveNaturalBlockedCell(position, cell, gridComponent, costs, out _, out float3 spawn)) continue;
                spawn.y = position.y;
                transform.ValueRW.Position = spawn;
                position = spawn;
            }
            // A production rally or an already-issued command owns its target.
            bool hasCommand = agent.ValueRO.hastarget || (em.HasComponent<MoveOverride>(entity) && em.IsComponentEnabled<MoveOverride>(entity));
            if (!hasCommand) MovementAgentAPI.SetTarget(em, entity, position, gridComponent, ecb);

            ecb.RemoveComponent<SetupUnitMoverDefaultPosition>(entity);
        }
        ecb.Playback(em);
        ecb.Dispose();
    }
}
