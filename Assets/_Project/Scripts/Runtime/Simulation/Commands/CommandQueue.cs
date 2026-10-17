using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public enum CommandType
{
    Move,
    TargetTo,
    Progression,
    Build,
    PlanBuild
}

public struct CommandQueueElement : IBufferElementData
{
    public int PlayerId;
    public CommandType Type;
    public GameBlobReference Progression;
    public Entity sourceEntity;
    public Entity targetEntity;
    public Entity SourcesEntity;
    public Vector3 position;
    public GridRect gridRect;
    public FogKnownTarget KnownTarget;
}

public struct CommandQueueComponent : IComponentData
{
}

public static class CommandDataHelper
{
    // Snapshot selection now; processing must not read a later UI selection.
    public static void AddPlanBuildToQueue(EntityManager em, int playerId,
        System.Collections.Generic.IReadOnlyList<Entity> sources, Entity prefab, Vector3 position)
    {
        using var query = em.CreateEntityQuery(typeof(CommandQueueComponent));
        var queue = query.GetSingletonEntity();
        var payload = em.CreateEntity();
        var buffer = em.AddBuffer<PlaceBuildingWorkerElement>(payload);
        foreach (var source in sources)
            buffer.Add(new PlaceBuildingWorkerElement { WorkerEntity = source });
        em.GetBuffer<CommandQueueElement>(queue).Add(new CommandQueueElement
        {
            PlayerId = playerId, Type = CommandType.PlanBuild, targetEntity = prefab,
            position = position, SourcesEntity = payload
        });
    }

    public static void AddCommandToQueue(
        EntityManager entityManager,
        int playerId,
        Entity sourceEntity,
        CommandType type,
        GameBlobReference progression = default,
        Entity targetEntity = default,
        Vector3 position = default,
        GridRect gridRect = default,
        FogKnownTarget knownTarget = default)
    {
        using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<CommandQueueComponent>());
        var commandQueueEntity = query.GetSingletonEntity();
        var commandBuffer = entityManager.GetBuffer<CommandQueueElement>(commandQueueEntity);
        commandBuffer.Add(new CommandQueueElement
        {
            PlayerId = playerId,
            Type = type,
            Progression = progression,
            sourceEntity = sourceEntity,
            targetEntity = targetEntity,
            position = position,
            gridRect = gridRect,
            KnownTarget = knownTarget,
        });
    }
}

partial struct CommandQueue : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        var e = state.EntityManager.CreateSingleton<CommandQueueComponent>();
        state.EntityManager.AddBuffer<CommandQueueElement>(e);
    }

    public void OnUpdate(ref SystemState state)
    {
        if (SystemAPI.TryGetSingleton<PlayerBootstrapState>(out var bootstrap) && bootstrap.Phase != PlayerBootstrapPhase.Ready) return;
        var e = SystemAPI.GetSingletonEntity<CommandQueueComponent>();
        // Placement and production can perform structural changes; do not retain a buffer across them.
        using var commands = state.EntityManager.GetBuffer<CommandQueueElement>(e).ToNativeArray(Allocator.Temp);
        state.EntityManager.GetBuffer<CommandQueueElement>(e).Clear();

        foreach (var command in commands)
        {
            ProcessCommand(ref state, command);
        }

    }

    private static void ProcessCommand(ref SystemState state, CommandQueueElement command)
    {
        if (command.Type == CommandType.PlanBuild)
        {
            HandlePlanBuild(ref state, command);
            return;
        }
        if (!CommandAuthority.CanControl(state.EntityManager, command.PlayerId, command.sourceEntity)
            || !IsValidSourceEntity(state.EntityManager, command))
            return;

        if (command.Type == CommandType.Build)
        {
            HandleBuild(ref state, command);
            return;
        }

        if (command.Type == CommandType.Progression)
        {
            HandleProgression(ref state, command);
            return;
        }

        DeactiveAllAbility(state.EntityManager, command.sourceEntity);

        switch (command.Type)
        {
            case CommandType.Move:
                HandleMove(ref state, command);
                break;
            case CommandType.TargetTo:
                HandleTargetTo(ref state, command);
                break;
        }
    }

    private static void HandlePlanBuild(ref SystemState state, CommandQueueElement command)
    {
        var em = state.EntityManager;
        var payload = command.SourcesEntity;
        if (payload == Entity.Null || !em.Exists(payload)) return;
        try
        {
        if (command.PlayerId < 0 || !em.HasBuffer<PlaceBuildingWorkerElement>(payload)
            || !em.Exists(command.targetEntity) || !em.HasComponent<Prefab>(command.targetEntity)
            || !em.HasComponent<BuildingComponent>(command.targetEntity)
            || !math.all(math.isfinite((float3)command.position))) return;
        var id = em.GetComponentData<BuildingComponent>(command.targetEntity).DefinitionID;
        using var workers = new NativeList<Entity>(Allocator.Temp);
        foreach (var source in em.GetBuffer<PlaceBuildingWorkerElement>(payload))
        {
            var worker = source.WorkerEntity;
            if (!CommandAuthority.CanControl(em, command.PlayerId, worker)
                || !em.HasComponent<BuilderComponent>(worker) || !em.HasComponent<MoveOverride>(worker)
                || !em.HasBuffer<BuildOffer>(worker)
                || (em.HasComponent<EntityHealth>(worker) && em.GetComponentData<EntityHealth>(worker).CurrentHP <= 0)) continue;
            bool allowed = false;
            foreach (var offer in em.GetBuffer<BuildOffer>(worker)) if (offer.DefinitionID == id) allowed = true;
            bool duplicate = false;
            foreach (var added in workers) if (added == worker) duplicate = true;
            if (allowed && !duplicate) workers.Add(worker);
        }
        if (workers.Length == 0) return;
        if (FogOrderRules.HasFog(em) && FogOrderRules.TryGrid(em, out _, out var grid)
            && em.HasComponent<BlockageData>(command.targetEntity)
            && PlayerSpawnPlacement.TryFootprint(grid, command.position,
                em.GetComponentData<BlockageData>(command.targetEntity).LocalRect, out var min, out var max)
            && !FogOrderRules.CellsVisible(em, command.PlayerId, min, max, true))
        {
            foreach (var worker in workers)
                BuilderSystem.EnqueuePlan(em, worker, command.targetEntity, command.position);
            return;
        }
        Entity reqEntity = em.CreateEntity();
        em.AddComponentData(reqEntity, new PlaceBuildingRequest
        {
            PlayerId = command.PlayerId,
            PrefabEntity = command.targetEntity,
            Position = command.position,
        });

        DynamicBuffer<PlaceBuildingWorkerElement> workerBuffer = em.AddBuffer<PlaceBuildingWorkerElement>(reqEntity);
        foreach (var worker in workers)
            workerBuffer.Add(new PlaceBuildingWorkerElement { WorkerEntity = worker });
        }
        finally { em.DestroyEntity(payload); }
    }

    private static bool IsValidSourceEntity(EntityManager entityManager, CommandQueueElement command)
    {
        if (command.sourceEntity == Entity.Null)
        {
            return false;
        }
        switch (command.Type)
        {
            case CommandType.Move:
                return entityManager.HasComponent<MoveOverride>(command.sourceEntity);

            case CommandType.Progression:
                return entityManager.HasComponent<ProductionData>(command.sourceEntity);

            case CommandType.Build:
                return entityManager.HasComponent<BuilderComponent>(command.sourceEntity) ||
                       entityManager.HasComponent<WorkerTag>(command.sourceEntity) ||
                       entityManager.HasComponent<ProductionData>(command.sourceEntity);

            case CommandType.TargetTo:
                return CanHandleTargetTo(entityManager, command);

            default:
                return false;
        }
    }

    private static bool CanHandleTargetTo(EntityManager entityManager, CommandQueueElement command)
    {
        if (command.KnownTarget.HasValue &&
            ((command.KnownTarget.Kind == FogRenderKind.Resource && entityManager.HasComponent<WorkerGatherData>(command.sourceEntity))
            || (command.KnownTarget.Kind == FogRenderKind.Building && entityManager.HasComponent<BuilderComponent>(command.sourceEntity))))
            return true;
        if (command.targetEntity == Entity.Null || !entityManager.Exists(command.targetEntity))
        {
            return false;
        }

        bool canGather =
            entityManager.HasComponent<MoveOverride>(command.sourceEntity) &&
            entityManager.HasComponent<WorkerGatherData>(command.sourceEntity) &&
            entityManager.HasComponent<ResourceNodeTag>(command.targetEntity);

        bool canAttack =
            entityManager.HasComponent<MoveOverride>(command.sourceEntity) &&
            entityManager.HasComponent<ShootAttack>(command.sourceEntity) &&
            entityManager.HasComponent<Target>(command.sourceEntity) &&
            entityManager.HasComponent<EntityHealth>(command.targetEntity);

        bool canBuild =
            entityManager.HasComponent<BuilderComponent>(command.sourceEntity) &&
            BuildingHelper.CanBuildOrRepair(entityManager, command.targetEntity);

        return canGather || canAttack || canBuild;
    }

    private static void HandleMove(ref SystemState state, CommandQueueElement command)
    {
        if (state.EntityManager.HasComponent<MoveOverride>(command.sourceEntity))
        {
            var moveOverrideData = state.EntityManager.GetComponentData<MoveOverride>(command.sourceEntity);
            moveOverrideData.targetPosition = command.position;
            moveOverrideData.targetApplied = false;
            state.EntityManager.SetComponentData(command.sourceEntity, moveOverrideData);
            state.EntityManager.SetComponentEnabled<MoveOverride>(command.sourceEntity, true);
        }

        if (state.EntityManager.HasComponent<TargetCache>(command.sourceEntity))
        {
            var targetCache = state.EntityManager.GetComponentData<TargetCache>(command.sourceEntity);
            targetCache.targetEntity = Entity.Null;
            targetCache.lastTargetPosition = command.position;
            state.EntityManager.SetComponentData(command.sourceEntity, targetCache);
        }
    }

    private static void HandleProgression(ref SystemState state, CommandQueueElement command)
    {
        if (!state.EntityManager.HasComponent<ProductionData>(command.sourceEntity))
        {
            return;
        }

        ProductionJobs.Enqueue(
            state.EntityManager,
            command.sourceEntity,
            command.Progression,
            command.PlayerId
        );
    }

    private static void HandleTargetTo(ref SystemState state, CommandQueueElement command)
    {
        if (command.KnownTarget.HasValue && command.KnownTarget.Kind == FogRenderKind.Resource
            && state.EntityManager.HasComponent<WorkerGatherData>(command.sourceEntity))
        { HandleGather(ref state, command); return; }
        if (command.KnownTarget.HasValue && command.KnownTarget.Kind == FogRenderKind.Building
            && state.EntityManager.HasComponent<BuilderComponent>(command.sourceEntity))
        { HandleBuild(ref state, command); return; }
        if (state.EntityManager.HasComponent<BuilderComponent>(command.sourceEntity)
            && EntityCapabilities.CanBuild(state.EntityManager, command.sourceEntity, command.targetEntity)
            && BuildingHelper.CanBuildOrRepair(state.EntityManager, command.targetEntity))
        { HandleBuild(ref state, command); return; }
        if (state.EntityManager.HasComponent<WorkerGatherData>(command.sourceEntity) &&
            state.EntityManager.HasComponent<ResourceNodeTag>(command.targetEntity))
        {
            HandleGather(ref state, command);
        }
        else if (state.EntityManager.HasComponent<ShootAttack>(command.sourceEntity) && state.EntityManager.HasComponent<EntityHealth>(command.targetEntity))
        {
            HandleAttack(ref state, command);
        }
        else if (state.EntityManager.HasComponent<BuilderComponent>(command.sourceEntity) &&
                 BuildingHelper.CanBuildOrRepair(state.EntityManager, command.targetEntity))
        {
            HandleBuild(ref state, command);
        }
    }

    private static void HandleBuild(ref SystemState state, CommandQueueElement command)
    {
        var em = state.EntityManager;
        if (!em.HasComponent<BuilderComponent>(command.sourceEntity))
        {
            return;
        }

        bool remembered = command.KnownTarget.HasValue && command.KnownTarget.Kind == FogRenderKind.Building
            && !FogOrderRules.IsVisible(em, command.PlayerId, command.targetEntity);
        if (!remembered && !FogOrderRules.IsVisible(em, command.PlayerId, command.targetEntity))
        { DeactiveAllAbility(em, command.sourceEntity); HandleMove(ref state, command); return; }
        if (!remembered && (command.targetEntity == Entity.Null || !em.Exists(command.targetEntity) || em.HasComponent<Prefab>(command.targetEntity)))
        {
            return;
        }

        if (!remembered && (!EntityCapabilities.CanBuild(em, command.sourceEntity, command.targetEntity) || !BuildingHelper.CanBuildOrRepair(em, command.targetEntity)))
            return;

        var builder = em.GetComponentData<BuilderComponent>(command.sourceEntity);
        if (command.Type == CommandType.Build &&
            em.IsComponentEnabled<BuilderComponent>(command.sourceEntity) &&
            builder.State != BuilderState.Idle && !builder.RepairOnly && !builder.WaitingForPlacement &&
            em.HasBuffer<BuilderQueueElement>(command.sourceEntity))
        {
            if (builder.TargetConstructionSite == command.targetEntity)
                return;
            var pending = em.GetBuffer<BuilderQueueElement>(command.sourceEntity);
            for (int i = 0; i < pending.Length; i++)
                if (pending[i].BuildingEntity == command.targetEntity)
                    return;
            pending.Add(new BuilderQueueElement { BuildingEntity = command.targetEntity, KnownTarget = command.KnownTarget });
            return;
        }

        if (!builder.WaitingForPlacement) DeactiveAllAbility(em, command.sourceEntity);
        builder = em.GetComponentData<BuilderComponent>(command.sourceEntity);
        builder.TargetConstructionSite = command.targetEntity;
        builder.PlannedPrefab = Entity.Null;
        builder.WaitingForPlacement = false;
        builder.RepairOnly = command.Type == CommandType.TargetTo;
        builder.KnownTarget = command.KnownTarget;
        builder.FogApproachRequested = false;
        builder.State = BuilderState.GoingToSite;
        em.SetComponentData(command.sourceEntity, builder);
        em.SetComponentEnabled<BuilderComponent>(command.sourceEntity, true);

        if (em.HasComponent<TargetCache>(command.sourceEntity))
        {
            var targetCache = em.GetComponentData<TargetCache>(command.sourceEntity);
            targetCache.targetEntity = command.targetEntity;
            if (remembered) targetCache.lastTargetPosition = command.KnownTarget.Position;
            else if (em.HasComponent<LocalTransform>(command.targetEntity))
            {
                targetCache.lastTargetPosition = em.GetComponentData<LocalTransform>(command.targetEntity).Position;
            }
            em.SetComponentData(command.sourceEntity, targetCache);
        }
    }

    private static void HandleAttack(ref SystemState state, CommandQueueElement command)
    {
        var em = state.EntityManager;
        bool enemy = em.HasComponent<EntityOwner>(command.targetEntity)
            && em.GetComponentData<EntityOwner>(command.targetEntity).PlayerID != command.PlayerId;
        using (var contexts = em.CreateEntityQuery(typeof(PlayerContext)))
        using (var players = contexts.ToComponentDataArray<PlayerContext>(Allocator.Temp))
            if (enemy && PlayerContextHelper.AreAllies(players, command.PlayerId,
                em.GetComponentData<EntityOwner>(command.targetEntity).PlayerID)) enemy = false;
        if (!enemy || !FogOrderRules.IsVisible(em, command.PlayerId, command.targetEntity))
        {
            if (em.HasComponent<Target>(command.sourceEntity)) em.SetComponentData(command.sourceEntity, new Target());
            HandleMove(ref state, command);
            return;
        }

        if (em.HasComponent<ShootAttack>(command.sourceEntity))
        {
            em.SetComponentEnabled<ShootAttack>(command.sourceEntity, true);
        }

        if (em.HasComponent<Target>(command.sourceEntity))
        {
            var targetData = em.GetComponentData<Target>(command.sourceEntity);
            targetData.targetEntity = command.targetEntity;
            em.SetComponentData(command.sourceEntity, targetData);
        }

        if (em.HasComponent<TargetCache>(command.sourceEntity))
        {
            var targetCache = em.GetComponentData<TargetCache>(command.sourceEntity);
            targetCache.targetEntity = command.targetEntity;
            if (em.HasComponent<Unity.Transforms.LocalTransform>(command.targetEntity))
            {
                targetCache.lastTargetPosition = em.GetComponentData<Unity.Transforms.LocalTransform>(command.targetEntity).Position;
            }
            em.SetComponentData(command.sourceEntity, targetCache);
        }
    }

    private static void HandleGather(ref SystemState state, CommandQueueElement command)
    {
        var gatherData = state.EntityManager.GetComponentData<WorkerGatherData>(command.sourceEntity);
        bool remembered = command.KnownTarget.HasValue &&
            !FogOrderRules.IsVisible(state.EntityManager, command.PlayerId, command.targetEntity);
        if (!remembered && !FogOrderRules.IsVisible(state.EntityManager, command.PlayerId, command.targetEntity))
        { HandleMove(ref state, command); return; }
        var resourceType = remembered ? command.KnownTarget.ResourceType
            : state.EntityManager.GetComponentData<ResourceNodeData>(command.targetEntity).Type;

        if (gatherData.CurrentResourceType != resourceType)
        {
            gatherData.CarryAmount = 0;
            gatherData.CurrentResourceType = resourceType;
        }

        gatherData.TargetNode = command.targetEntity;
        gatherData.KnownTarget = command.KnownTarget;
        gatherData.TargetDepot = Entity.Null;
        gatherData.GatherTimer = 0f;
        gatherData.State = WorkerGatherState.GoingToNode;
        state.EntityManager.SetComponentData(command.sourceEntity, gatherData);
        state.EntityManager.SetComponentEnabled<WorkerGatherData>(command.sourceEntity, true);
        if (remembered) FogOrderRules.BeginTravel(state.EntityManager, command.sourceEntity, command.PlayerId, command.KnownTarget);

        if (state.EntityManager.HasComponent<TargetCache>(command.sourceEntity))
        {
            var targetCache = state.EntityManager.GetComponentData<TargetCache>(command.sourceEntity);
            targetCache.targetEntity = command.targetEntity;
            if (remembered) targetCache.lastTargetPosition = command.KnownTarget.Position;
            else if (state.EntityManager.HasComponent<Unity.Transforms.LocalTransform>(command.targetEntity))
            {
                float3 rawPos = state.EntityManager.GetComponentData<Unity.Transforms.LocalTransform>(command.targetEntity).Position;
                targetCache.lastTargetPosition = state.EntityManager.HasComponent<BlockageData>(command.targetEntity)
                    ? state.EntityManager.GetComponentData<BlockageData>(command.targetEntity).GetWorldCenter(rawPos)
                    : rawPos;
            }
            state.EntityManager.SetComponentData(command.sourceEntity, targetCache);
        }
    }

    public static void DeactiveAllAbility(EntityManager em, Entity entity)
    {
        if (em.HasComponent<BuilderComponent>(entity))
        {
            var builder = em.GetComponentData<BuilderComponent>(entity);
            if (builder.WaitingForPlacement && em.Exists(builder.PlacementRequest)) em.DestroyEntity(builder.PlacementRequest);
            builder.WaitingForPlacement = false; builder.PlannedPrefab = Entity.Null; builder.KnownTarget = default;
            em.SetComponentData(entity, builder);
        }
        if (em.HasComponent<FogTargetTravel>(entity)) em.RemoveComponent<FogTargetTravel>(entity);
        if (em.HasComponent<Target>(entity)) em.SetComponentData(entity, new Target());
        if (em.HasBuffer<BuilderQueueElement>(entity))
            em.GetBuffer<BuilderQueueElement>(entity).Clear();
        if (em.HasComponent<MoveOverride>(entity))
            em.SetComponentEnabled<MoveOverride>(entity, false);

        if (em.HasComponent<WorkerGatherData>(entity))
        {
            em.SetComponentEnabled<WorkerGatherData>(entity, false);
        }

        if (em.HasComponent<BuilderComponent>(entity))
        {
            BuilderSystem.RestoreStoppingDistance(em, entity);
            em.SetComponentEnabled<BuilderComponent>(entity, false);
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
    }
}
