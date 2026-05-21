using System.Diagnostics;
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
        UnityEngine.Debug.Log("CommandQueue OnUpdate"+commandBuffer.Length);
        foreach (var command in commandBuffer)
        {
            if (command.sourceEntity == Entity.Null)
            {
                continue; // Skip commands with null source entity
            }
            if (state.EntityManager.GetBuffer<CommandElement>(command.sourceEntity).Length > 0)
            {
                bool hasSameCommand = false;
                foreach (var unitcommand in state.EntityManager.GetBuffer<CommandElement>(command.sourceEntity))
                {
                    if (command.Command.Type == unitcommand.Type)
                    {
                        if (command.Command.Type == CommandType.Progression)
                        {
                            if (command.Command.indexInUnitCommandList==unitcommand.indexInUnitCommandList)
                            {
                                hasSameCommand = true;
                            }
                        }
                        else
                        {
                            hasSameCommand = true;
                        }
                    }
                }
                if (!hasSameCommand)
                {
                    continue; //skip if source entity can not receive the command
                }
            }
            if (command.Command.Type == CommandType.Move)
            {
                // Handle move command
            }
            else if (command.Command.Type == CommandType.Progression)
            {
                if (state.EntityManager.HasComponent<ProductionData>(command.sourceEntity))
                {
                    TrainUnitHelper.TrainUnit(entityManager: state.EntityManager, buildingEntity: command.sourceEntity, indexInPrefabList: command.Command.indexInUnitCommandList);
                }
                UnityEngine.Debug.Log("Processing Progression command"+command.sourceEntity);
            }
            else if (command.Command.Type == CommandType.Build)
            {
                // Handle build command
            }
            else if (command.Command.Type == CommandType.TargetTo)
            {
                if (state.EntityManager.HasComponent<WorkerTag>(command.sourceEntity))
                {
                    if(state.EntityManager.HasComponent<WorkerGatherData>(command.sourceEntity))
                    {
                        // Handle target to command for workers with gather data
                        var gatherData = state.EntityManager.GetComponentData<WorkerGatherData>(command.sourceEntity);
                        gatherData.TargetNode = command.targetEntity;
                    }
                }
            }
        }
        commandBuffer.Clear();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
