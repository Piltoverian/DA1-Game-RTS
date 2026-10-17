using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(CommandQueue))]
[UpdateBefore(typeof(ConstructionSystem))]

public partial struct BuilderSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        using var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (builderData, localTransform, entity) in SystemAPI.Query<RefRW<BuilderComponent>, RefRO<LocalTransform>>().WithNone<FogTargetTravel>().WithEntityAccess())
        {
            if (builderData.ValueRO.KnownTarget.HasValue)
            {
                int player = state.EntityManager.GetComponentData<EntityOwner>(entity).PlayerID;
                var known = builderData.ValueRO.KnownTarget;
                if (!FogOrderRules.KnownAreaVisible(state.EntityManager, player, known))
                {
                    if (builderData.ValueRO.FogApproachRequested && !state.EntityManager.IsComponentEnabled<MoveOverride>(entity)
                        && math.distancesq(localTransform.ValueRO.Position.xz, math.clamp(localTransform.ValueRO.Position.xz, known.Min, known.Max))
                            > builderData.ValueRO.BuildRange * builderData.ValueRO.BuildRange)
                    { StopBuilder(state.EntityManager, builderData, entity); continue; }
                    if (!FogOrderRules.Approach(state.EntityManager, entity, localTransform.ValueRO.Position, known.Min, known.Max, builderData.ValueRO.BuildRange))
                    { builderData.ValueRW.FogApproachRequested = true; continue; }
                }
            }
            if (builderData.ValueRO.WaitingForPlacement)
            {
                if (!state.EntityManager.Exists(builderData.ValueRO.PlacementRequest))
                {
                    builderData.ValueRW.WaitingForPlacement = false;
                    builderData.ValueRW.PlannedPrefab = Entity.Null;
                    StopBuilder(state.EntityManager, builderData, entity);
                }
                continue;
            }
            if (builderData.ValueRO.PlannedPrefab != Entity.Null)
            {
                UpdatePlan(state.EntityManager, builderData, entity, localTransform.ValueRO.Position, ecb);
                continue;
            }
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
                            RestoreStoppingDistance(state.EntityManager, builderData, entity);
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
                                if (TryApproach(state.EntityManager, entity, constructionSite, localTransform.ValueRO.Position,
                                    builderData.ValueRO.BuildRange, out var approach, out float stopDistance))
                                {
                                    if (state.EntityManager.HasComponent<MovementSteeringComponent>(entity))
                                    {
                                        var steering = state.EntityManager.GetComponentData<MovementSteeringComponent>(entity);
                                        if (!builderData.ValueRO.HasApproachStoppingDistance)
                                        {
                                            builderData.ValueRW.PreviousStoppingDistance = steering.stoppingDistance;
                                            builderData.ValueRW.HasApproachStoppingDistance = true;
                                        }
                                        steering.stoppingDistance = math.min(builderData.ValueRO.PreviousStoppingDistance, stopDistance);
                                        state.EntityManager.SetComponentData(entity, steering);
                                    }
                                    moveOverride.targetPosition = approach;
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
                        using var repairCosts = new NativeList<ResourcePair>(Allocator.Temp);
                        for (int c = 0; c < costs.Length; c++)
                            repairCosts.Add(new ResourcePair(costs[c].Type, costs[c].Amount * repairRatio));
                        if (!ResourcePayment.TryPay(em, playerId, repairCosts, out var repairPaid))
                        {
                            repairPaid.Dispose();
                            break;
                        }
                        repairPaid.Dispose();

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
                            StopBuilder(em, builderData, entity);
                    }
                    break;
            }
        }
        ecb.Playback(state.EntityManager);

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

    public static void EnqueuePlan(EntityManager em, Entity worker, Entity prefab, float3 position)
    {
        var builder = em.GetComponentData<BuilderComponent>(worker);
        if (em.IsComponentEnabled<BuilderComponent>(worker) && builder.State != BuilderState.Idle && !builder.RepairOnly)
        {
            if (em.HasBuffer<BuilderQueueElement>(worker)) em.GetBuffer<BuilderQueueElement>(worker).Add(
                new BuilderQueueElement { Prefab = prefab, Position = position });
            return;
        }
        CommandQueue.DeactiveAllAbility(em, worker);
        builder = em.GetComponentData<BuilderComponent>(worker);
        builder.RepairOnly = false; builder.KnownTarget = default; builder.FogApproachRequested = false;
        builder.TargetConstructionSite = Entity.Null;
        builder.PlannedPrefab = prefab; builder.PlannedPosition = position;
        builder.WaitingForPlacement = false; builder.State = BuilderState.GoingToSite;
        em.SetComponentData(worker, builder); em.SetComponentEnabled<BuilderComponent>(worker, true);
        if (em.HasComponent<TargetCache>(worker)) em.SetComponentData(worker, new TargetCache { lastTargetPosition = position });
    }

    private static void UpdatePlan(EntityManager em, RefRW<BuilderComponent> builder, Entity worker, float3 position, EntityCommandBuffer ecb)
    {
        var prefab = builder.ValueRO.PlannedPrefab;
        if (!em.Exists(prefab) || !em.HasComponent<BlockageData>(prefab))
        { builder.ValueRW.PlannedPrefab = Entity.Null; StopBuilder(em, builder, worker); return; }
        var root = builder.ValueRO.PlannedPosition;
        var rect = em.GetComponentData<BlockageData>(prefab).LocalRect;
        int player = em.GetComponentData<EntityOwner>(worker).PlayerID;
        bool visible = FogOrderRules.TryGrid(em, out _, out var grid)
            && PlayerSpawnPlacement.TryFootprint(grid, root, rect, out var min, out var max)
            && FogOrderRules.CellsVisible(em, player, min, max, true);
        if (!visible)
        {
            if (builder.ValueRO.FogApproachRequested && !em.IsComponentEnabled<MoveOverride>(worker)
                && math.distancesq(position.xz, math.clamp(position.xz, root.xz + rect.MinPoint, root.xz + rect.MaxPoint))
                    > builder.ValueRO.BuildRange * builder.ValueRO.BuildRange)
            { builder.ValueRW.PlannedPrefab = Entity.Null; StopBuilder(em, builder, worker); return; }
            if (!FogOrderRules.Approach(em, worker, position, root.xz + rect.MinPoint, root.xz + rect.MaxPoint, builder.ValueRO.BuildRange))
            { builder.ValueRW.FogApproachRequested = true; return; }
        }
        Entity request = ecb.CreateEntity();
        ecb.AddComponent(request, new PlaceBuildingRequest { PlayerId = player, PrefabEntity = prefab, Position = root });
        ecb.AddBuffer<PlaceBuildingWorkerElement>(request).Add(new PlaceBuildingWorkerElement { WorkerEntity = worker });
        var data = builder.ValueRO;
        data.WaitingForPlacement = true; data.PlacementRequest = request;
        ecb.SetComponent(worker, data); // ECB remaps the deferred request entity.
        if (em.HasComponent<MoveOverride>(worker)) em.SetComponentEnabled<MoveOverride>(worker, false);
    }

    private static bool TryNearbyBuilding(EntityManager em, RefRW<BuilderComponent> builder, Entity worker)
    {
        if (!FogOrderRules.TryGrid(em, out var gridEntity, out var grid) || !em.HasComponent<EntityOwner>(worker)) return false;
        int player = em.GetComponentData<EntityOwner>(worker).PlayerID;
        float3 center = em.GetComponentData<LocalTransform>(worker).Position;
        if (em.HasComponent<TargetCache>(worker))
        {
            var last = em.GetComponentData<TargetCache>(worker).lastTargetPosition;
            if (math.lengthsq(last) > .001f) center = last;
        }
        using var query = em.CreateEntityQuery(typeof(BuildingConstruction), typeof(BuildingComponent), typeof(LocalTransform), typeof(EntityHealth));
        using var sites = query.ToEntityArray(Allocator.Temp);
        Entity best = Entity.Null; float distance = float.MaxValue;
        foreach (var site in sites)
        {
            var construction = em.GetComponentData<BuildingConstruction>(site);
            var definition = em.GetComponentData<BuildingComponent>(site);
            if ((construction.Phase != ConstructionPhase.Planned && construction.Phase != ConstructionPhase.UnderConstruction)
                || construction.CompletedWork >= definition.WorkLoad || em.GetComponentData<EntityHealth>(site).CurrentHP <= 0
                || !EntityCapabilities.CanBuild(em, worker, site) || !FogOrderRules.IsVisible(em, player, site)
                || !FogOrderRules.IsNearbyReachable(em, worker, site, center, grid, gridEntity)) continue;
            var pos = em.GetComponentData<LocalTransform>(site).Position;
            if (em.HasComponent<BlockageData>(site)) pos = em.GetComponentData<BlockageData>(site).GetWorldCenter(pos);
            float d = math.distancesq(pos.xz, center.xz);
            if (d < distance) { best = site; distance = d; }
        }
        if (best == Entity.Null) return false;
        builder.ValueRW.TargetConstructionSite = best; builder.ValueRW.State = BuilderState.GoingToSite;
        builder.ValueRW.PlannedPrefab = Entity.Null; builder.ValueRW.KnownTarget = default; builder.ValueRW.FogApproachRequested = false;
        if (em.HasComponent<TargetCache>(worker)) em.SetComponentData(worker, new TargetCache {
            targetEntity = best, lastTargetPosition = em.GetComponentData<LocalTransform>(best).Position });
        return true;
    }

    private static bool TryStartNextBuilding(EntityManager em, RefRW<BuilderComponent> builder, Entity entity)
    {
        if (builder.ValueRO.RepairOnly) return false;
        if (!em.HasBuffer<BuilderQueueElement>(entity))
            return false;
        var pending = em.GetBuffer<BuilderQueueElement>(entity);
        while (pending.Length > 0)
        {
            var next = pending[0];
            Entity site = next.BuildingEntity;
            pending.RemoveAt(0);
            builder.ValueRW.KnownTarget = default; builder.ValueRW.FogApproachRequested = false;
            if (next.Prefab != Entity.Null)
            {
                builder.ValueRW.PlannedPrefab = next.Prefab;
                builder.ValueRW.PlannedPosition = next.Position;
                builder.ValueRW.TargetConstructionSite = Entity.Null;
                builder.ValueRW.State = BuilderState.GoingToSite;
                return true;
            }
            if (next.KnownTarget.HasValue && !FogOrderRules.IsVisible(em, em.GetComponentData<EntityOwner>(entity).PlayerID, site))
            {
                builder.ValueRW.TargetConstructionSite = site;
                builder.ValueRW.State = BuilderState.GoingToSite;
                // Approach the frozen position before consulting the live site.
                builder.ValueRW.KnownTarget = next.KnownTarget;
                return true;
            }
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
            builder.ValueRW.KnownTarget = next.KnownTarget;
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
        RestoreStoppingDistance(em, builder, entity);
        FogOrderRules.StopMove(em, entity);
        builder.ValueRW.FogApproachRequested = false;
        if (TryStartNextBuilding(em, builder, entity))
            return;
        if (!builder.ValueRO.RepairOnly && TryNearbyBuilding(em, builder, entity)) return;
        builder.ValueRW.State = BuilderState.Idle;
        builder.ValueRW.TargetConstructionSite = Entity.Null;
        builder.ValueRW.KnownTarget = default;
        em.SetComponentEnabled<BuilderComponent>(entity, false);
    }

    private static void RestoreStoppingDistance(EntityManager em, RefRW<BuilderComponent> builder, Entity entity)
    {
        RestoreStoppingDistance(em, entity);
        builder.ValueRW.HasApproachStoppingDistance = false;
    }

    public static void RestoreStoppingDistance(EntityManager em, Entity entity)
    {
        var builder = em.GetComponentData<BuilderComponent>(entity);
        if (!builder.HasApproachStoppingDistance) return;
        if (em.HasComponent<MovementSteeringComponent>(entity))
        {
            var steering = em.GetComponentData<MovementSteeringComponent>(entity);
            steering.stoppingDistance = builder.PreviousStoppingDistance;
            em.SetComponentData(entity, steering);
        }
        builder.HasApproachStoppingDistance = false;
        em.SetComponentData(entity, builder);
    }

    // Clamp to the nearest footprint point, then leave room for the worker collider.
    private static bool TryApproach(EntityManager em, Entity worker, Entity site, float3 position, float range,
        out float3 goal, out float stopDistance)
    {
        goal = default; stopDistance = 0;
        if (!math.isfinite(range) || range <= 0) return false;
        var rect = em.GetComponentData<BlockageData>(site).LocalRect;
        var centre = em.GetComponentData<LocalTransform>(site).Position;
        float2 minimum = centre.xz + rect.MinPoint, maximum = centre.xz + rect.MaxPoint;
        float2 closest = math.clamp(position.xz, minimum, maximum);
        float radius = em.HasComponent<MovementAgentAvoidanceComponent>(worker)
            ? em.GetComponentData<MovementAgentAvoidanceComponent>(worker).radius : 0;
        float clearance = math.max(0, radius) + .01f;
        if (clearance >= range) return false;
        float2 approach = closest + math.normalizesafe(position.xz - closest) * clearance;
        goal = new float3(approach.x, position.y, approach.y);
        stopDistance = math.max(.01f, (range - clearance) * .5f);
        return true;
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
            if (status == ConstructionPhase.Completed && !builder.ValueRO.RepairOnly)
            { StopBuilder(em, builder, entity); return true; }
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

