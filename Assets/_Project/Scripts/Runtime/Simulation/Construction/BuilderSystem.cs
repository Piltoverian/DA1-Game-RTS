using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(CommandQueue))]
[UpdateBefore(typeof(ConstructionSystem))]

partial struct BuilderSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (builderData, localTransform, entity) in SystemAPI.Query<RefRW<BuilderComponent>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            if (state.EntityManager.HasComponent<EntityWork>(entity))
                builderData.ValueRW.BuildWorkLoadPerSecond = state.EntityManager.GetComponentData<EntityWork>(entity).Rate;
            switch (builderData.ValueRO.State)
            {
                case BuilderState.Idle:
                    StopBuilder(state.EntityManager, builderData, entity);
                    break;

                case BuilderState.GoingToSite:
                    if (CheckConstructionSiteAndReturnToIdle(ref state, builderData, entity))
                    {
                        break;
                    }

                    var constructionSite = builderData.ValueRO.TargetConstructionSite;
                    if (state.EntityManager.HasComponent<BlockageData>(constructionSite) && state.EntityManager.HasComponent<LocalTransform>(constructionSite))
                    {
                        var buildingTransform = state.EntityManager.GetComponentData<LocalTransform>(constructionSite);
                        float2 closestPoint = GetClosestPointFromConstruction(ref state, localTransform, constructionSite);
                        float rangeSq = builderData.ValueRO.BuildRange * builderData.ValueRO.BuildRange;

                        if (math.distancesq(localTransform.ValueRO.Position.xz, closestPoint) <= rangeSq)
                        {
                            if (state.EntityManager.HasComponent<MoveOverride>(entity))
                            {
                                state.EntityManager.SetComponentEnabled<MoveOverride>(entity, false);
                            }
                            builderData.ValueRW.State = BuilderState.Building;
                        }
                        else
                        {
                            if (state.EntityManager.HasComponent<MoveOverride>(entity) && !state.EntityManager.IsComponentEnabled<MoveOverride>(entity))
                            {
                                var moveOverride = state.EntityManager.GetComponentData<MoveOverride>(entity);
                                if (math.distancesq(moveOverride.targetPosition.xz, buildingTransform.Position.xz) > 0.01f)
                                {
                                    moveOverride.targetPosition = buildingTransform.Position;
                                    moveOverride.targetApplied = false;
                                    state.EntityManager.SetComponentData(entity, moveOverride);
                                    state.EntityManager.SetComponentEnabled<MoveOverride>(entity, true);
                                }
                            }
                        }
                    }
                    break;

                case BuilderState.Building:
                    if (CheckConstructionSiteAndReturnToIdle(ref state, builderData, entity))
                    {
                        break;
                    }

                    constructionSite = builderData.ValueRO.TargetConstructionSite;
                    if (state.EntityManager.HasComponent<BlockageData>(constructionSite) && state.EntityManager.HasComponent<LocalTransform>(constructionSite))
                    {
                        float2 closestPoint = GetClosestPointFromConstruction(ref state, localTransform, constructionSite);
                        float rangeSq = builderData.ValueRO.BuildRange * builderData.ValueRO.BuildRange;

                        if (math.distancesq(localTransform.ValueRO.Position.xz, closestPoint) > rangeSq)
                        {
                            builderData.ValueRW.State = BuilderState.GoingToSite;
                            if (state.EntityManager.HasComponent<MoveOverride>(entity))
                            {
                                var mo = state.EntityManager.GetComponentData<MoveOverride>(entity);
                                mo.targetPosition = new float3(float.MaxValue, 0, float.MaxValue);
                                state.EntityManager.SetComponentData(entity, mo);
                            }
                            break;
                        }
                    }

                    var em = state.EntityManager;
                    var health = em.GetComponentData<EntityHealth>(constructionSite);
                    var building = em.GetComponentData<BuildingComponent>(constructionSite);
                    float work = math.max(0f, builderData.ValueRO.BuildWorkLoadPerSecond * SystemAPI.Time.DeltaTime);
                    float hpPerWork = health.MaxHP / building.WorkLoad;

                    if (em.GetComponentData<BuildingConstruction>(constructionSite).Phase == ConstructionPhase.Completed)
                    {
                        float restoredHp = math.min(work * hpPerWork, health.MaxHP - health.CurrentHP);
                        if (restoredHp <= 0f)
                            break;

                        int playerId = em.GetComponentData<EntityOwner>(constructionSite).PlayerID;
                        float repairRatio = restoredHp / health.MaxHP;
                        var costs = em.GetBuffer<EntityResourceCost>(constructionSite);
                        bool canAfford = true;
                        for (int c = 0; c < costs.Length; c++)
                        {
                            if (PlayerContextHelper.GetPlayerResourceByType(em, playerId, costs[c].Type, out float available) != FunctionResult.Success ||
                                available < costs[c].Amount * repairRatio)
                            {
                                canAfford = false;
                                break;
                            }
                        }
                        if (!canAfford)
                            break;

                        for (int c = 0; c < costs.Length; c++)
                            PlayerContextHelper.AddPlayerResource(em, playerId, costs[c].Type, -costs[c].Amount * repairRatio);

                        health.CurrentHP = math.min(health.MaxHP, health.CurrentHP + restoredHp);
                        health.Changed = true;
                        em.SetComponentData(constructionSite, health);
                        if (health.CurrentHP >= health.MaxHP)
                            StopBuilder(em, builderData, entity);
                    }
                    else
                    {
                        var construction = em.GetComponentData<BuildingConstruction>(constructionSite);
                        work = math.min(work, math.max(0f, building.WorkLoad - construction.CompletedWork));
                        construction.CompletedWork = math.min(building.WorkLoad, construction.CompletedWork + work);
                        health.CurrentHP = math.min(health.MaxHP, health.CurrentHP + work * hpPerWork);
                        health.Changed = true;
                        em.SetComponentData(constructionSite, construction);
                        em.SetComponentData(constructionSite, health);
                        if (construction.CompletedWork >= building.WorkLoad)
                            TryStartNextBuilding(em, builderData, entity);
                    }
                    break;
            }
        }

        static float2 GetClosestPointFromConstruction(ref SystemState state, RefRO<LocalTransform> localTransform, Entity constructionSite)
        {
            var blockageData = state.EntityManager.GetComponentData<BlockageData>(constructionSite);
            var buildingTransform = state.EntityManager.GetComponentData<LocalTransform>(constructionSite);
            float2 worldMin = buildingTransform.Position.xz + blockageData.LocalRect.MinPoint;
            float2 worldMax = buildingTransform.Position.xz + blockageData.LocalRect.MaxPoint;
            float2 closestPoint = math.clamp(localTransform.ValueRO.Position.xz, worldMin, worldMax);
            return closestPoint;
        }
    }

    private static bool TryStartNextBuilding(EntityManager em, RefRW<BuilderComponent> builder, Entity entity)
    {
        if (!em.HasBuffer<BuilderQueueElement>(entity))
            return false;
        var pending = em.GetBuffer<BuilderQueueElement>(entity);
        while (pending.Length > 0)
        {
            Entity site = pending[0].BuildingEntity;
            pending.RemoveAt(0);
            if (!EntityCapabilities.CanBuild(em, entity, site) || !em.HasComponent<BuildingConstruction>(site) ||
                !em.HasComponent<BuildingComponent>(site) || !em.HasComponent<BuildingConstruction>(site) ||
                !em.HasComponent<EntityHealth>(site) || !em.HasComponent<LocalTransform>(site) ||
                !em.HasComponent<BlockageData>(site))
                continue;
            var status = em.GetComponentData<BuildingConstruction>(site).Phase;
            float total = em.GetComponentData<BuildingComponent>(site).WorkLoad;
            if ((status != ConstructionPhase.Planned && status != ConstructionPhase.UnderConstruction) ||
                !math.isfinite(total) || total <= 0f ||
                em.GetComponentData<BuildingConstruction>(site).CompletedWork >= total ||
                em.GetComponentData<EntityHealth>(site).CurrentHP <= 0f)
                continue;

            builder.ValueRW.TargetConstructionSite = site;
            builder.ValueRW.State = BuilderState.GoingToSite;
            float3 position = em.GetComponentData<LocalTransform>(site).Position;
            if (em.HasComponent<MoveOverride>(entity))
            {
                var move = em.GetComponentData<MoveOverride>(entity);
                move.targetPosition = position;
                move.targetApplied = false;
                em.SetComponentData(entity, move);
                em.SetComponentEnabled<MoveOverride>(entity, true);
            }
            if (em.HasComponent<TargetCache>(entity))
            {
                var target = em.GetComponentData<TargetCache>(entity);
                target.targetEntity = site;
                target.lastTargetPosition = position;
                em.SetComponentData(entity, target);
            }
            return true;
        }
        return false;
    }

    private static void StopBuilder(EntityManager em, RefRW<BuilderComponent> builder, Entity entity)
    {
        if (TryStartNextBuilding(em, builder, entity))
            return;
        builder.ValueRW.State = BuilderState.Idle;
        builder.ValueRW.TargetConstructionSite = Entity.Null;
        em.SetComponentEnabled<BuilderComponent>(entity, false);
        if (em.HasComponent<MoveOverride>(entity))
            em.SetComponentEnabled<MoveOverride>(entity, false);
    }

    private static bool CheckConstructionSiteAndReturnToIdle(ref SystemState state, RefRW<BuilderComponent> builder, Entity entity)
    {
        var em = state.EntityManager;
        Entity site = builder.ValueRO.TargetConstructionSite;
        bool valid = EntityCapabilities.CanBuild(em, entity, site) && em.HasComponent<BuildingConstruction>(site) &&
                     em.HasComponent<EntityHealth>(site) && em.HasComponent<BuildingComponent>(site);
        if (valid)
        {
            var status = em.GetComponentData<BuildingConstruction>(site).Phase;
            var health = em.GetComponentData<EntityHealth>(site);
            float total = em.GetComponentData<BuildingComponent>(site).WorkLoad;
            valid = math.isfinite(total) && total > 0f && health.CurrentHP > 0f && health.MaxHP > 0f;
            if (status == ConstructionPhase.Completed && TryStartNextBuilding(em, builder, entity))
                return true;
            if (status == ConstructionPhase.Completed)
                valid &= health.CurrentHP < health.MaxHP &&
                         em.HasComponent<EntityOwner>(site) && em.HasBuffer<EntityResourceCost>(site);
            else
                valid &= (status == ConstructionPhase.Planned || status == ConstructionPhase.UnderConstruction) &&
                         em.HasComponent<BuildingConstruction>(site);
        }
        if (valid)
            return false;
        StopBuilder(em, builder, entity);
        return true;
    }
}

