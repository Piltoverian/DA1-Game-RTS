using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
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
            Debug.Log($"Processing command: {command.Command.Type}");
            ProcessCommand(ref state, command);
        }

        commandBuffer.Clear();
    }

    private static void ProcessCommand(ref SystemState state, CommandQueueElement command)
    {
        if (!IsValidSourceEntity(state.EntityManager, command))
        {
            return;
        }

        DeactiveAllAbility(state.EntityManager, command.sourceEntity);

        switch (command.Command.Type)
        {
            case CommandType.Move:
                HandleMove(ref state, command);
                break;
            case CommandType.Progression:
                HandleProgression(ref state, command);
                break;
            case CommandType.TargetTo:
                HandleTargetTo(ref state, command);
                break;
            case CommandType.Build:
                HandleBuild(ref state, command);
                break;
        }
    }

    private static bool IsValidSourceEntity(EntityManager entityManager, CommandQueueElement command)
    {
        if (command.sourceEntity == Entity.Null)
        {
            return false;
        }
        switch (command.Command.Type)
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
            entityManager.HasComponent<Health>(command.targetEntity);

        bool canBuild =
            entityManager.HasComponent<BuilderComponent>(command.sourceEntity) &&
            entityManager.HasComponent<UnderConstructionTag>(command.targetEntity);
        
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

        PlayerContext playerContext = new PlayerContext();
        PlayerContextHelper.GetContextData(state.EntityManager, command.PlayerId, out playerContext);
        if (playerContext.currentPopulation >= playerContext.maxPopulation)
        {
            return;
        }
        TrainUnitHelper.TrainUnit(
            entityManager: state.EntityManager,
            buildingEntity: command.sourceEntity,
            indexInPrefabList: command.Command.indexInUnitCommandList
        );
    }

    private static void HandleTargetTo(ref SystemState state, CommandQueueElement command)
    {
        if (state.EntityManager.HasComponent<WorkerGatherData>(command.sourceEntity) &&
            state.EntityManager.HasComponent<ResourceNodeTag>(command.targetEntity))
        {
            HandleGather(ref state, command);
        }
        else if (state.EntityManager.HasComponent<ShootAttack>(command.sourceEntity) && state.EntityManager.HasComponent<Health>(command.targetEntity))
        {
            HandleAttack(ref state, command);
        }
        else if (state.EntityManager.HasComponent<BuilderComponent>(command.sourceEntity) && state.EntityManager.HasComponent<UnderConstructionTag>(command.targetEntity))
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

        var builder = em.GetComponentData<BuilderComponent>(command.sourceEntity);
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
                targetCache.lastTargetPosition = state.EntityManager.GetComponentData<Unity.Transforms.LocalTransform>(command.targetEntity).Position;
            }
            state.EntityManager.SetComponentData(command.sourceEntity, targetCache);
        }
    }
    
    public static void DeactiveAllAbility(EntityManager em,Entity entity)
    {
        if (em.HasComponent<MoveOverride>(entity))
            em.SetComponentEnabled<MoveOverride>(entity, false);
        
        if (em.HasComponent<WorkerGatherData>(entity))
        {
            em.SetComponentEnabled<WorkerGatherData>(entity, false);
        }

        if (em.HasComponent<ShootAttack>(entity))
        {
            em.SetComponentEnabled<ShootAttack>(entity, false);
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
