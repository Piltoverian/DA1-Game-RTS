using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
[BurstCompile]
public partial struct ResetTargetSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (target, findTarget, transform) in
            SystemAPI.Query<
                RefRW<Target>,
                RefRO<FindTarget>,
                RefRO<LocalTransform>
            >())
        {
            Entity targetEntity = target.ValueRO.targetEntity;

            if (targetEntity == Entity.Null || !SystemAPI.Exists(targetEntity))
            {
                target.ValueRW.targetEntity = Entity.Null;
                continue;
            }

            var targetTransform =
                SystemAPI.GetComponent<LocalTransform>(targetEntity);

            float distanceSq = math.distancesq(
                transform.ValueRO.Position,
                targetTransform.Position);

            if (distanceSq > findTarget.ValueRO.range * findTarget.ValueRO.range)
            {
                target.ValueRW.targetEntity = Entity.Null;
            }
        }
    }
}