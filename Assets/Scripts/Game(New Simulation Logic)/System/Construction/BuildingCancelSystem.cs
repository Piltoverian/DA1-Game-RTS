using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(SimulationSystemGroup))]
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

            float refundRate = 0f;
            if (bState.Current != BuildingState.Completed)
            {
                float progress = 0f;
                if (em.HasComponent<Health>(req.BuildingEntity))
                {
                    var health = em.GetComponentData<Health>(req.BuildingEntity);
                    progress = math.saturate(health.healthAmount / math.max(1f, health.maxHealthAmount));
                }
                else if (em.HasComponent<BuildingData>(req.BuildingEntity) && em.HasComponent<ConstructionData>(req.BuildingEntity))
                {
                    float total = em.GetComponentData<BuildingData>(req.BuildingEntity).TotalWorkLoad;
                    float current = em.GetComponentData<ConstructionData>(req.BuildingEntity).currentWorkLoad;
                    progress = math.saturate(current / math.max(0.001f, total));
                }

                refundRate = 1.0f - progress;
            }

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

                if (em.HasComponent<BuildingStateComponent>(req.BuildingEntity))
                {
                    var st = em.GetComponentData<BuildingStateComponent>(req.BuildingEntity);
                    st.Previous = st.Current;
                    st.Current = BuildingState.Destroyed;
                    em.SetComponentData(req.BuildingEntity, st);
                }

                if (em.HasBuffer<LinkedEntityGroup>(req.BuildingEntity))
                {
                    var linkedGroup = em.GetBuffer<LinkedEntityGroup>(req.BuildingEntity);
                    for (int j = linkedGroup.Length - 1; j >= 0; j--)
                    {
                        Entity child = linkedGroup[j].Value;
                        if (child != req.BuildingEntity && em.Exists(child))
                        {
                            ecb.DestroyEntity(child);
                        }
                    }
                }

                ecb.DestroyEntity(req.BuildingEntity);
                ecb.DestroyEntity(reqEntity);
            }

        ecb.Playback(em);
        ecb.Dispose();
    }
}
