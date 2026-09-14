using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(BuilderSystem))]
[UpdateBefore(typeof(ConstructionSystem))]
public partial struct BuildingCancelSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (reqRef, reqEntity) in SystemAPI.Query<RefRO<CancelBuildingRequest>>().WithEntityAccess())
        {
            var req = reqRef.ValueRO;

            if (req.BuildingEntity == Entity.Null || !em.Exists(req.BuildingEntity))
            {
                ecb.DestroyEntity(reqEntity);
                continue;
            }

            if (!em.HasComponent<BuildingStateComponent>(req.BuildingEntity))
            {
                ecb.DestroyEntity(reqEntity);
                continue;
            }

            var bState = em.GetComponentData<BuildingStateComponent>(req.BuildingEntity);
            if (bState.Current == BuildingState.Destroyed)
            {
                ecb.DestroyEntity(reqEntity);
                continue;
            }

            if (em.HasComponent<Unit>(req.BuildingEntity))
            {
                var unit = em.GetComponentData<Unit>(req.BuildingEntity);
                if (unit.playerID != req.PlayerId)
                {
                    ecb.DestroyEntity(reqEntity);
                    continue;
                }
            }

            // Construction progress is independent of damage. Completed buildings never refund their build cost.
            float refundRate = 0f;
            if ((bState.Current == BuildingState.StartBuild || bState.Current == BuildingState.UnderConstruction) &&
                em.HasComponent<BuildingData>(req.BuildingEntity) &&
                em.HasComponent<ConstructionData>(req.BuildingEntity))
            {
                float total = em.GetComponentData<BuildingData>(req.BuildingEntity).TotalWorkLoad;
                float current = em.GetComponentData<ConstructionData>(req.BuildingEntity).currentWorkLoad;
                if (math.isfinite(total) && total > 0f && math.isfinite(current))
                    refundRate = 1f - math.saturate(current / total);
            }

            // Mark immediately so duplicate cancellation requests cannot refund the same building twice.
            bState.Previous = bState.Current;
            bState.Current = BuildingState.Destroyed;
            em.SetComponentData(req.BuildingEntity, bState);

            if (refundRate > 0f && em.HasBuffer<BuildingCost>(req.BuildingEntity))
            {
                var costBuffer = em.GetBuffer<BuildingCost>(req.BuildingEntity);
                for (int c = 0; c < costBuffer.Length; c++)
                {
                    float refundAmount = costBuffer[c].Amount * refundRate;
                    if (refundAmount > 0f)
                    {
                        PlayerContextHelper.AddPlayerResource(em, req.PlayerId, costBuffer[c].Type, refundAmount);
                    }
                }
            }

            // Queued units were paid separately; their refund does not depend on construction progress.
            if (em.HasComponent<ProductionData>(req.BuildingEntity) && em.HasBuffer<ProductionQueueElement>(req.BuildingEntity))
            {
                var prod = em.GetComponentData<ProductionData>(req.BuildingEntity);
                var queue = em.GetBuffer<ProductionQueueElement>(req.BuildingEntity);
                int count = queue.Length;
                if (count > 0)
                {
                    if (prod.UnitGoldCost > 0)
                        PlayerContextHelper.AddPlayerResource(em, req.PlayerId, ResourceType.Gold, prod.UnitGoldCost * count);
                    if (prod.UnitFoodCost > 0)
                        PlayerContextHelper.AddPlayerResource(em, req.PlayerId, ResourceType.Food, prod.UnitFoodCost * count);
                }
            }

            // Destroying the root also destroys its LinkedEntityGroup.
            ecb.DestroyEntity(req.BuildingEntity);
            ecb.DestroyEntity(reqEntity);
        }

        ecb.Playback(em);
        ecb.Dispose();
    }
}
