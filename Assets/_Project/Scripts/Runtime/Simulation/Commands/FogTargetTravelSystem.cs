using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
[UpdateAfter(typeof(CommandQueue))]
[UpdateBefore(typeof(BuilderSystem))]
[UpdateBefore(typeof(WorkerGatherSystem))]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial struct FogTargetTravelSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        if (!ServerMatch.isGamePlayCanRun(ref state)) return;
        var em = state.EntityManager;
        using var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (travel, transform, entity) in SystemAPI.Query<RefRW<FogTargetTravel>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            var known = travel.ValueRO.Known;
            bool visible = FogOrderRules.KnownAreaVisible(em, travel.ValueRO.PlayerId, known);
            bool failed = !visible && travel.ValueRO.ApproachRequested
                && math.distancesq(transform.ValueRO.Position.xz, math.clamp(transform.ValueRO.Position.xz, known.Min, known.Max)) > .25f
                && (!em.HasComponent<MoveOverride>(entity) || !em.IsComponentEnabled<MoveOverride>(entity));
            if (visible || failed || KnownTargetOrders.Approach(em, entity, transform.ValueRO.Position, known.Min, known.Max, .5f))
            {
                if (failed && em.HasComponent<WorkerGatherData>(entity))
                {
                    var gather = em.GetComponentData<WorkerGatherData>(entity);
                    gather.TargetNode = Entity.Null; gather.KnownTarget = default;
                    gather.State = WorkerGatherState.GoingToNode; em.SetComponentData(entity, gather);
                }
                ecb.RemoveComponent<FogTargetTravel>(entity);
                KnownTargetOrders.StopMove(em, entity);
            }
            else travel.ValueRW.ApproachRequested = true;
        }
        ecb.Playback(em);
    }
}
