using Unity.Burst;
using Unity.Entities;
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
                return entityManager.HasComponent<WorkerTag>(command.sourceEntity) ||
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

        return canGather || canAttack;
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

        if (state.EntityManager.HasComponent<WorkerGatherData>(command.sourceEntity))
        {
            var gatherData = state.EntityManager.GetComponentData<WorkerGatherData>(command.sourceEntity);
            gatherData.TargetNode = Entity.Null;
            gatherData.TargetDepot = Entity.Null;
            gatherData.CarryAmount = 0;
            gatherData.GatherTimer = 0f;
            gatherData.State = WorkerGatherState.GoingToNode;
            state.EntityManager.SetComponentData(command.sourceEntity, gatherData);
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
       
    }

    private static void HandleAttack(ref SystemState state, CommandQueueElement command)
    {
        if (!state.EntityManager.HasComponent<Target>(command.sourceEntity))
        {
            return;
        }

        var targetData = state.EntityManager.GetComponentData<Target>(command.sourceEntity);
        targetData.targetEntity = command.targetEntity;
        state.EntityManager.SetComponentData(command.sourceEntity, targetData);
        state.EntityManager.SetComponentEnabled<MoveOverride>(command.sourceEntity, false);
    }

    private static void HandleGather(ref SystemState state, CommandQueueElement command)
    {
        var gatherData = state.EntityManager.GetComponentData<WorkerGatherData>(command.sourceEntity);
        gatherData.TargetNode = command.targetEntity;
        gatherData.TargetDepot = Entity.Null;
        gatherData.CarryAmount = 0;
        gatherData.GatherTimer = 0f;
        gatherData.State = WorkerGatherState.GoingToNode;
        state.EntityManager.SetComponentData(command.sourceEntity, gatherData);
        state.EntityManager.SetComponentEnabled<MoveOverride>(command.sourceEntity, false);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
