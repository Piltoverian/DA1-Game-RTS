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
    Build
}

public struct CommandQueueElement : IBufferElementData
{
    public int PlayerId;
    public CommandType Type;
    public int DataIndex;
    public Entity sourceEntity;
    public Entity targetEntity;
    public Vector3 position;
    public GridRect gridRect;
}

public struct CommandQueueComponent : IComponentData
{
}

public static class CommandDataHelper
{
    public static void AddCommandToQueue(
        EntityManager entityManager,
        int playerId,
        Entity sourceEntity,
        CommandType type,
        int dataIndex = 0,
        Entity targetEntity = default,
        Vector3 position = default,
        GridRect gridRect = default)
    {
        var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<CommandQueueComponent>());
        var commandQueueEntity = query.GetSingletonEntity();
        var commandBuffer = entityManager.GetBuffer<CommandQueueElement>(commandQueueEntity);
        commandBuffer.Add(new CommandQueueElement
        {
            PlayerId = playerId,
            Type = type,
            DataIndex = dataIndex,
            sourceEntity = sourceEntity,
            targetEntity = targetEntity,
            position = position,
            gridRect = gridRect,
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
        var e = SystemAPI.GetSingletonEntity<CommandQueueComponent>();
        var commandBuffer = state.EntityManager.GetBuffer<CommandQueueElement>(e);

        foreach (var command in commandBuffer)
        {
            ProcessCommand(ref state, command);
        }

        commandBuffer.Clear();
    }

    private static void ProcessCommand(ref SystemState state, CommandQueueElement command)
    {
        if (command.Type == CommandType.Build)
        {
            if (command.targetEntity != Entity.Null &&
                state.EntityManager.HasComponent<BuildingComponent>(command.targetEntity) &&
                !state.EntityManager.HasComponent<BuildingConstruction>(command.targetEntity))
            {
                HandlePlaceBuilding(ref state, command);
                return;
            }

            HandleBuild(ref state, command);
            return;
        }

        if (command.Type == CommandType.Progression)
        {
            HandleProgression(ref state, command);
            return;
        }

        if (!IsValidSourceEntity(state.EntityManager, command))
        {
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

    private static void HandlePlaceBuilding(ref SystemState state, CommandQueueElement command)
    {
        var em = state.EntityManager;
        Entity reqEntity = em.CreateEntity();
        em.AddComponentData(reqEntity, new PlaceBuildingRequest
        {
            PlayerId = command.PlayerId,
            PrefabEntity = command.targetEntity,
            Position = command.position,
        });

        DynamicBuffer<PlaceBuildingWorkerElement> workerBuffer = em.AddBuffer<PlaceBuildingWorkerElement>(reqEntity);
        if (command.sourceEntity != Entity.Null && em.Exists(command.sourceEntity))
        {
            workerBuffer.Add(new PlaceBuildingWorkerElement { WorkerEntity = command.sourceEntity });
        }
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
        if (command.targetEntity == Entity.Null)
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
            command.DataIndex
        );
    }

    private static void HandleTargetTo(ref SystemState state, CommandQueueElement command)
    {
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

        if (command.targetEntity == Entity.Null || !em.Exists(command.targetEntity))
        {
            return;
        }

        if (!EntityCapabilities.CanBuild(em, command.sourceEntity, command.targetEntity) || !BuildingHelper.CanBuildOrRepair(em, command.targetEntity))
            return;

        var builder = em.GetComponentData<BuilderComponent>(command.sourceEntity);
        if (command.Type == CommandType.Build &&
            em.IsComponentEnabled<BuilderComponent>(command.sourceEntity) &&
            builder.State != BuilderState.Idle &&
            em.HasBuffer<BuilderQueueElement>(command.sourceEntity))
        {
            if (builder.TargetConstructionSite == command.targetEntity)
                return;
            var pending = em.GetBuffer<BuilderQueueElement>(command.sourceEntity);
            for (int i = 0; i < pending.Length; i++)
                if (pending[i].BuildingEntity == command.targetEntity)
                    return;
            pending.Add(new BuilderQueueElement { BuildingEntity = command.targetEntity });
            return;
        }

        DeactiveAllAbility(em, command.sourceEntity);
        builder.TargetConstructionSite = command.targetEntity;
        builder.State = BuilderState.GoingToSite;
        em.SetComponentData(command.sourceEntity, builder);
        em.SetComponentEnabled<BuilderComponent>(command.sourceEntity, true);

        if (em.HasComponent<TargetCache>(command.sourceEntity))
        {
            var targetCache = em.GetComponentData<TargetCache>(command.sourceEntity);
            targetCache.targetEntity = command.targetEntity;
            if (em.HasComponent<LocalTransform>(command.targetEntity))
            {
                targetCache.lastTargetPosition = em.GetComponentData<LocalTransform>(command.targetEntity).Position;
            }
            em.SetComponentData(command.sourceEntity, targetCache);
        }
    }

    private static void HandleAttack(ref SystemState state, CommandQueueElement command)
    {
        var em = state.EntityManager;

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
        var nodeData = state.EntityManager.GetComponentData<ResourceNodeData>(command.targetEntity);

        if (gatherData.CurrentResourceType != nodeData.Type)
        {
            gatherData.CarryAmount = 0;
            gatherData.CurrentResourceType = nodeData.Type;
        }

        gatherData.TargetNode = command.targetEntity;
        gatherData.TargetDepot = Entity.Null;
        gatherData.GatherTimer = 0f;
        gatherData.State = WorkerGatherState.GoingToNode;
        state.EntityManager.SetComponentData(command.sourceEntity, gatherData);
        state.EntityManager.SetComponentEnabled<WorkerGatherData>(command.sourceEntity, true);

        if (state.EntityManager.HasComponent<TargetCache>(command.sourceEntity))
        {
            var targetCache = state.EntityManager.GetComponentData<TargetCache>(command.sourceEntity);
            targetCache.targetEntity = command.targetEntity;
            if (state.EntityManager.HasComponent<Unity.Transforms.LocalTransform>(command.targetEntity))
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
            em.SetComponentEnabled<BuilderComponent>(entity, false);
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
    }
}
