using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(CommandQueue))]
[UpdateBefore(typeof(ConstructionSystem))]

partial struct BuilderSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        
    }

    [BurstCompile]
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

                    var constructionData = state.EntityManager.GetComponentData<ConstructionData>(constructionSite);
                    var buildingData = state.EntityManager.GetComponentData<BuildingData>(constructionSite);

                    constructionData.currentWorkLoad += builderData.ValueRO.BuildWorkLoadPerSecond * SystemAPI.Time.DeltaTime;
                    constructionData.currentWorkLoad = math.clamp(constructionData.currentWorkLoad, 0f, buildingData.TotalWorkLoad);

                    if (constructionData.currentWorkLoad >= buildingData.TotalWorkLoad)
                    {
                        builderData.ValueRW.State = BuilderState.Idle;
                        builderData.ValueRW.TargetConstructionSite = Entity.Null;
                        state.EntityManager.SetComponentEnabled<BuilderComponent>(entity, false);
                        if (state.EntityManager.HasComponent<MoveOverride>(entity))
                        {
                            state.EntityManager.SetComponentEnabled<MoveOverride>(entity, false);
                        }
                    }

                    state.EntityManager.SetComponentData(constructionSite, constructionData);
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
        if (site == Entity.Null || !state.EntityManager.Exists(site) || !state.EntityManager.HasComponent<UnderConstructionTag>(site))
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
        return false;
    }


    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
