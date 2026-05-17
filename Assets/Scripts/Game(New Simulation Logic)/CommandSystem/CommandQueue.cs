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
            if (command.Command.Type == CommandType.Move)
            {
                // Handle move command
            }
            else if (command.Command.Type == CommandType.Progression)
            {
                if (state.EntityManager.HasComponent<ProductionData>(command.targetEntity))
                {
                    TrainUnitHelper.TrainUnit(entityManager: state.EntityManager, buildingEntity: command.targetEntity, indexInPrefabList: command.Command.indexInUnitCommandList);
                }
                UnityEngine.Debug.Log("Processing Progression command"+command.targetEntity);
            }
            else if (command.Command.Type == CommandType.Build)
            {
                // Handle build command
            }
        }
        commandBuffer.Clear();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
