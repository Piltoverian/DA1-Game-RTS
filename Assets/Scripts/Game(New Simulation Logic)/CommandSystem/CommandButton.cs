using Unity.Entities;
using UnityEngine;

public class CommandButton : MonoBehaviour
{
    [SerializeField] private CommandData commandData;

    public void OnClick()
    {
        Debug.Log(
            $"Command Button Clicked! Type: {commandData.Type}, Index: {commandData.indexInUnitCommandList}"
        );

        int playerId = GameManager.Instance.GetModule<SelectManager>().currentContext.playerId;
        Entity sourceEntity = SelectHelper.GetFirstSelectedEntityByplayerID(playerId);

        switch (commandData.Type)
        {
            case CommandType.Move:
                break;

            case CommandType.Progression:
                {
                    if (sourceEntity == Entity.Null)
                    {
                        Debug.LogWarning("No selected entity for Progression command.");
                        return;
                    }

                    var world = World.DefaultGameObjectInjectionWorld;
                    if (world == null)
                    {
                        Debug.LogError("DefaultGameObjectInjectionWorld is null.");
                        return;
                    }

                    var entityManager = world.EntityManager;
                    CommandDataHelper.AddCommandToQueue(
                        entityManager: entityManager,
                        playerId: playerId,
                        sourceEntity: sourceEntity,
                        commandData: commandData
                    );

                    break;
                }
            case CommandType.Build:
                {
                    if (sourceEntity == Entity.Null)
                    {
                        Debug.LogWarning("No selected entity for Build command.");
                        return;
                    }

                    if (BuildingPlacer.Instance == null)
                    {
                        Debug.LogError("BuildingPlacer.Instance is null.");
                        return;
                    }

                    BuildingPlacer.Instance.StartPlacementFromCommand(commandData, sourceEntity);
                    break;
                }

            case CommandType.TargetTo:
                // Handle target to command
                break;
            default:
                Debug.LogWarning("Unknown command type.");
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
