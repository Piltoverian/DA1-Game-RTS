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
                    var health = em.GetComponentData<Health>(constructionSite);
                    var building = em.GetComponentData<BuildingData>(constructionSite);
                    float work = math.max(0f, builderData.ValueRO.BuildWorkLoadPerSecond * SystemAPI.Time.DeltaTime);
                    float hpPerWork = health.maxHealthAmount / building.TotalWorkLoad;

                    if (em.GetComponentData<BuildingStateComponent>(constructionSite).Current == BuildingState.Completed)
                    {
                        float restoredHp = math.min(work * hpPerWork, health.maxHealthAmount - health.healthAmount);
                        if (restoredHp <= 0f)
                            break;

                        int playerId = em.GetComponentData<Unit>(constructionSite).playerID;
                        float repairRatio = restoredHp / health.maxHealthAmount;
                        var costs = em.GetBuffer<BuildingCost>(constructionSite);
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

                        health.healthAmount = math.min(health.maxHealthAmount, health.healthAmount + restoredHp);
                        health.OnHealthChanged = true;
                        em.SetComponentData(constructionSite, health);
                        if (health.healthAmount >= health.maxHealthAmount)
                            StopBuilder(em, builderData, entity);
                    }
                    else
                    {
                        var construction = em.GetComponentData<ConstructionData>(constructionSite);
                        work = math.min(work, math.max(0f, building.TotalWorkLoad - construction.currentWorkLoad));
                        construction.currentWorkLoad = math.min(building.TotalWorkLoad, construction.currentWorkLoad + work);
                        health.healthAmount = math.min(health.maxHealthAmount, health.healthAmount + work * hpPerWork);
                        health.OnHealthChanged = true;
                        em.SetComponentData(constructionSite, construction);
                        em.SetComponentData(constructionSite, health);
                        if (construction.currentWorkLoad >= building.TotalWorkLoad)
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
            if (!em.Exists(site) || !em.HasComponent<BuildingStateComponent>(site) ||
                !em.HasComponent<BuildingData>(site) || !em.HasComponent<ConstructionData>(site) ||
                !em.HasComponent<Health>(site) || !em.HasComponent<LocalTransform>(site) ||
                !em.HasComponent<BlockageData>(site))
                continue;
            var status = em.GetComponentData<BuildingStateComponent>(site).Current;
            float total = em.GetComponentData<BuildingData>(site).TotalWorkLoad;
            if ((status != BuildingState.StartBuild && status != BuildingState.UnderConstruction) ||
                !math.isfinite(total) || total <= 0f ||
                em.GetComponentData<ConstructionData>(site).currentWorkLoad >= total ||
                em.GetComponentData<Health>(site).healthAmount <= 0f)
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
        bool valid = em.Exists(site) && em.HasComponent<BuildingStateComponent>(site) &&
                     em.HasComponent<Health>(site) && em.HasComponent<BuildingData>(site);
        if (valid)
        {
            var status = em.GetComponentData<BuildingStateComponent>(site).Current;
            var health = em.GetComponentData<Health>(site);
            float total = em.GetComponentData<BuildingData>(site).TotalWorkLoad;
            valid = math.isfinite(total) && total > 0f && health.healthAmount > 0f && health.maxHealthAmount > 0f;
            if (status == BuildingState.Completed && TryStartNextBuilding(em, builder, entity))
                return true;
            if (status == BuildingState.Completed)
                valid &= health.healthAmount < health.maxHealthAmount &&
                         em.HasComponent<Unit>(site) && em.HasBuffer<BuildingCost>(site);
            else
                valid &= (status == BuildingState.StartBuild || status == BuildingState.UnderConstruction) &&
                         em.HasComponent<ConstructionData>(site);
        }
        if (valid)
            return false;
        StopBuilder(em, builder, entity);
        return true;
    }
}
