using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(CommandQueue))]
[UpdateBefore(typeof(ConstructionSystem))]

partial struct BuilderSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        
    }

    public void OnUpdate(ref SystemState state)
    {
        foreach (var (builderData, localTransform, entity) in SystemAPI.Query<RefRW<BuilderComponent>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            switch (builderData.ValueRO.State)
            {
                case BuilderState.Idle:
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

                    if (!state.EntityManager.HasComponent<Health>(constructionSite))
                    {
                        break;
                    }

                    var bState = state.EntityManager.GetComponentData<BuildingStateComponent>(constructionSite);
                    var health = state.EntityManager.GetComponentData<Health>(constructionSite);
                    float dt = SystemAPI.Time.DeltaTime;
                    float deltaHealth = builderData.ValueRO.BuildWorkLoadPerSecond * dt;

                    if (bState.Current == BuildingState.Completed)
                    {
                        if (health.healthAmount >= health.maxHealthAmount)
                        {
                            builderData.ValueRW.State = BuilderState.Idle;
                            builderData.ValueRW.TargetConstructionSite = Entity.Null;
                            state.EntityManager.SetComponentEnabled<BuilderComponent>(entity, false);
                            if (state.EntityManager.HasComponent<MoveOverride>(entity))
                            {
                                state.EntityManager.SetComponentEnabled<MoveOverride>(entity, false);
                            }
                            break;
                        }

                        int playerId = 0;
                        if (state.EntityManager.HasComponent<Unit>(entity))
                        {
                            playerId = state.EntityManager.GetComponentData<Unit>(entity).playerID;
                        }
                        else if (state.EntityManager.HasComponent<Unit>(constructionSite))
                        {
                            playerId = state.EntityManager.GetComponentData<Unit>(constructionSite).playerID;
                        }

                        float repairRatio = deltaHealth / math.max(1f, health.maxHealthAmount);
                        bool canAfford = true;

                        if (state.EntityManager.HasBuffer<BuildingCost>(constructionSite))
                        {
                            var costBuffer = state.EntityManager.GetBuffer<BuildingCost>(constructionSite);
                            for (int c = 0; c < costBuffer.Length; c++)
                            {
                                float costNeeded = costBuffer[c].Amount * repairRatio;
                                PlayerContextHelper.GetPlayerResourceByType(state.EntityManager, playerId, costBuffer[c].Type, out float playerRes);
                                if (playerRes < costNeeded)
                                {
                                    canAfford = false;
                                    break;
                                }
                            }

                            if (canAfford)
                            {
                                for (int c = 0; c < costBuffer.Length; c++)
                                {
                                    float costNeeded = costBuffer[c].Amount * repairRatio;
                                    PlayerContextHelper.AddPlayerResource(state.EntityManager, playerId, costBuffer[c].Type, -costNeeded);
                                }
                            }
                        }

                        if (canAfford)
                        {
                            health.healthAmount = math.min(health.maxHealthAmount, health.healthAmount + deltaHealth);
                            health.OnHealthChanged = true;
                            state.EntityManager.SetComponentData(constructionSite, health);

                            if (health.healthAmount >= health.maxHealthAmount)
                            {
                                builderData.ValueRW.State = BuilderState.Idle;
                                builderData.ValueRW.TargetConstructionSite = Entity.Null;
                                state.EntityManager.SetComponentEnabled<BuilderComponent>(entity, false);
                                if (state.EntityManager.HasComponent<MoveOverride>(entity))
                                {
                                    state.EntityManager.SetComponentEnabled<MoveOverride>(entity, false);
                                }
                            }
                        }
                    }
                    else
                    {
                        health.healthAmount = math.min(health.maxHealthAmount, health.healthAmount + deltaHealth);
                        health.OnHealthChanged = true;
                        state.EntityManager.SetComponentData(constructionSite, health);

                        if (state.EntityManager.HasComponent<ConstructionData>(constructionSite))
                        {
                            var cData = state.EntityManager.GetComponentData<ConstructionData>(constructionSite);
                            cData.currentWorkLoad = health.healthAmount;
                            state.EntityManager.SetComponentData(constructionSite, cData);
                        }

                        if (health.healthAmount >= health.maxHealthAmount)
                        {
                            builderData.ValueRW.State = BuilderState.Idle;
                            builderData.ValueRW.TargetConstructionSite = Entity.Null;
                            state.EntityManager.SetComponentEnabled<BuilderComponent>(entity, false);
                            if (state.EntityManager.HasComponent<MoveOverride>(entity))
                            {
                                state.EntityManager.SetComponentEnabled<MoveOverride>(entity, false);
                            }
                        }
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

    private static bool CheckConstructionSiteAndReturnToIdle(ref SystemState state, RefRW<BuilderComponent> builderData, Entity entity)
    {
        Entity site = builderData.ValueRO.TargetConstructionSite;
        if (site == Entity.Null || !state.EntityManager.Exists(site) || !state.EntityManager.HasComponent<BuildingStateComponent>(site))
        {
            builderData.ValueRW.State = BuilderState.Idle;
            builderData.ValueRW.TargetConstructionSite = Entity.Null;
            state.EntityManager.SetComponentEnabled<BuilderComponent>(entity, false);
            if (state.EntityManager.HasComponent<MoveOverride>(entity))
            {
                state.EntityManager.SetComponentEnabled<MoveOverride>(entity, false);
            }
            return true;
        }

        var bState = state.EntityManager.GetComponentData<BuildingStateComponent>(site);
        if (bState.Current == BuildingState.Destroyed)
        {
            builderData.ValueRW.State = BuilderState.Idle;
            builderData.ValueRW.TargetConstructionSite = Entity.Null;
            state.EntityManager.SetComponentEnabled<BuilderComponent>(entity, false);
            if (state.EntityManager.HasComponent<MoveOverride>(entity))
            {
                state.EntityManager.SetComponentEnabled<MoveOverride>(entity, false);
            }
            return true;
        }

        if (state.EntityManager.HasComponent<Health>(site))
        {
            var h = state.EntityManager.GetComponentData<Health>(site);
            if (bState.Current == BuildingState.Completed && h.healthAmount >= h.maxHealthAmount)
            {
                builderData.ValueRW.State = BuilderState.Idle;
                builderData.ValueRW.TargetConstructionSite = Entity.Null;
                state.EntityManager.SetComponentEnabled<BuilderComponent>(entity, false);
                if (state.EntityManager.HasComponent<MoveOverride>(entity))
                {
                    state.EntityManager.SetComponentEnabled<MoveOverride>(entity, false);
                }
                return true;
            }
        }

        return false;
    }


    public void OnDestroy(ref SystemState state)
    {
        
    }
}
