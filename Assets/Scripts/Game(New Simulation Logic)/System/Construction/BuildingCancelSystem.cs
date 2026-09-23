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

            if (!em.HasComponent<BuildingConstruction>(req.BuildingEntity))
            {
                ecb.DestroyEntity(reqEntity);
                continue;
            }

            var bState = em.GetComponentData<BuildingConstruction>(req.BuildingEntity);
            if (bState.Phase == ConstructionPhase.Destroyed)
            {
                ecb.DestroyEntity(reqEntity);
                continue;
            }

            if (em.HasComponent<EntityOwner>(req.BuildingEntity))
            {
                var unit = em.GetComponentData<EntityOwner>(req.BuildingEntity);
                if (unit.PlayerID != req.PlayerId)
                {
                    ecb.DestroyEntity(reqEntity);
                    continue;
                }
            }

            // Construction progress is independent of damage. Completed buildings never refund their build cost.
            float refundRate = 0f;
            if ((bState.Phase == ConstructionPhase.Planned || bState.Phase == ConstructionPhase.UnderConstruction) &&
                em.HasComponent<BuildingComponent>(req.BuildingEntity) &&
                em.HasComponent<BuildingConstruction>(req.BuildingEntity))
            {
                float total = em.GetComponentData<BuildingComponent>(req.BuildingEntity).WorkLoad;
                float current = em.GetComponentData<BuildingConstruction>(req.BuildingEntity).CompletedWork;
                if (math.isfinite(total) && total > 0f && math.isfinite(current))
                    refundRate = 1f - math.saturate(current / total);
            }

            // Mark immediately so duplicate cancellation requests cannot refund the same building twice.
            bState.Phase = ConstructionPhase.Destroyed;
            em.SetComponentData(req.BuildingEntity, bState);

            if (refundRate > 0f && em.HasBuffer<EntityResourceCost>(req.BuildingEntity))
            {
                var costBuffer = em.GetBuffer<EntityResourceCost>(req.BuildingEntity);
                for (int c = 0; c < costBuffer.Length; c++)
                {
                    float refundAmount = costBuffer[c].Amount * refundRate;
                    if (refundAmount > 0f)
                    {
                        PlayerContextHelper.AddPlayerResource(em, req.PlayerId, costBuffer[c].Type, refundAmount);
                    }
                }
            }

            ProductionJobs.CancelAll(em, req.BuildingEntity);

            // Destroying the root also destroys its LinkedEntityGroup.
            ecb.DestroyEntity(req.BuildingEntity);
            ecb.DestroyEntity(reqEntity);
        }

        ecb.Playback(em);
        ecb.Dispose();
    }
}

