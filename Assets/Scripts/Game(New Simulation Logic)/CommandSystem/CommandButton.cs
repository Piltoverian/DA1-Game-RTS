using Unity.Entities;
using UnityEngine;

public class CommandButton : MonoBehaviour
{
    [SerializeField] private CommandData commandData;

    public void OnClick()
    {
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
                        return;
                    }

                    if (BuildingPlacer.Instance == null)
                    {
                        Debug.LogError("BuildingPlacer.Instance is null.");
                        return;
                    }

                    BuildingPlacer.Instance.StartPlacementFromCommand(commandData, sourceEntity,playerId);
                    break;
                }

            case CommandType.TargetTo:
                // Handle target to command
                break;
            default:
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
