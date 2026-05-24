using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

public class CommandListAuthoring : MonoBehaviour
{
    public List<CommandData> CommandList;

    public class Baker : Baker<CommandListAuthoring>
    {
        public override void Bake(CommandListAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);
            DynamicBuffer<CommandElement> commandDataBuffer = AddBuffer<CommandElement>(entity);
            foreach (var commandData in authoring.CommandList)
            {
                CommandElement commandElement = new CommandElement
                {
                    Type = commandData.Type,
                    indexInUnitCommandList = commandData.indexInUnitCommandList
                };

                commandDataBuffer.Add(commandElement);
            }
        }
    }
}
public enum CommandType
{
    Move,
    TargetTo,
    Progression,//Do progression like build, upgrade, research, etc.
    Build
}
[System.Serializable]
public struct CommandData
{
    public CommandType Type;
    public int indexInUnitCommandList;
    
}

public struct CommandElement:IBufferElementData
{
    public CommandType Type;
    public int indexInUnitCommandList;
}

public struct CommandQueueElement : IBufferElementData
{
    public CommandData Command;
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
    public static void AddCommandToQueue(EntityManager entityManager,Entity sourceEntity ,CommandData commandData, Entity targetEntity = default, Vector3 position = default, GridRect gridRect = default)
    {
        var query=entityManager.CreateEntityQuery(ComponentType.ReadOnly<CommandQueueComponent>());
        var commandQueueEntity = query.GetSingletonEntity();
        var commandBuffer = entityManager.GetBuffer<CommandQueueElement>(commandQueueEntity);
        commandBuffer.Add(new CommandQueueElement
        {
            Command = commandData,
            sourceEntity = sourceEntity,
            targetEntity = targetEntity,
            position = position,
            gridRect = gridRect,
        });
    }

    public static NativeList<CommandElement> GetCommandsForEntity(EntityManager entityManager, Entity targetEntity)
    {
        var commandBuffer = entityManager.GetBuffer<CommandElement>(targetEntity);
        NativeList<CommandElement> commandsForEntity = new NativeList<CommandElement>(Allocator.Temp);
        foreach (var command in commandBuffer)
        {
                commandsForEntity.Add(command);
        }
        return commandsForEntity;
    }
}
