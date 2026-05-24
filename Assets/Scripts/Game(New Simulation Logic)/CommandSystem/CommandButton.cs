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
                break;
            case CommandType.Progression:
                // Handle progression command
                var world = World.DefaultGameObjectInjectionWorld;
                var entityManager = world.EntityManager;
                var CurrentPlayerContext=GameManager.Instance.GetModule<SelectManager>().currentContext;
                CommandDataHelper.AddCommandToQueue(entityManager: entityManager, sourceEntity: SelectHelper.GetFirstSelectedEntityByplayerID(CurrentPlayerContext.playerId), commandData: commandData);
                break;
            case CommandType.Build:
                // Handle build command
                break;
            case CommandType.TargetTo:
                // Handle target to command
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
