using UnityEngine;
using Unity.Entities;

public class CommandButton : MonoBehaviour
{
    [SerializeField]private CommandData commandData;
    public void OnClick()
    {
        Debug.Log($"Command Button Clicked! Command Type: {commandData.Type}, Index in Unit Command List: {commandData.indexInUnitCommandList}");
        switch (commandData.Type)
        {
            case CommandType.Move:
                // Handle move command
                break;
            case CommandType.Progression:
                // Handle progression command
                var world = World.DefaultGameObjectInjectionWorld;
                var entityManager = world.EntityManager;
                CommandDataHelper.AddCommandToQueue(entityManager: entityManager, targetEntity: SelectHelper.GetFirstSelectedEntity(), commandData: commandData);
                break;
            case CommandType.Build:
                // Handle build command
                break;
            default:
                Debug.LogWarning("Unknown command type!");
                break;
        }
    }

    public void SetCommandDataFromCommandData(CommandData data)
    {
        commandData = data;
    }

    public void SetCommandDataFromBufferElement(CommandElement commandElement)
    {
        commandData = new CommandData
        {
            Type = commandElement.Type,
            indexInUnitCommandList = commandElement.indexInUnitCommandList
        };
    }
}
