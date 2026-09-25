using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public class CommandButton : MonoBehaviour
{
    [SerializeField] private CommandType commandType;
    [SerializeField] private int dataIndex;
    private FixedString64Bytes buildingId;
    private FixedString64Bytes jobId;

    public void OnClick()
    {
        int playerId = GameManager.Instance.GetModule<SelectManager>().currentContext.playerId;
        Entity sourceEntity = SelectHelper.GetFirstSelectedEntityByplayerID(playerId);

        switch (commandType)
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
                        type: CommandType.Progression,
                        dataIndex: dataIndex
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

                    if (!buildingId.IsEmpty)
                    {
                        BuildingPlacer.Instance.StartPlacement(buildingId, sourceEntity, playerId);
                    }
                    else
                    {
                        BuildingPlacer.Instance.StartPlacementFromOfferIndex(dataIndex, sourceEntity, playerId);
                    }
                    break;
                }

            case CommandType.TargetTo:
                break;
            default:
                break;
        }
    }

    public void SetBuildOffer(FixedString64Bytes definitionId, int index = 0)
    {
        buildingId = definitionId;
        jobId = default;
        commandType = CommandType.Build;
        dataIndex = index;
    }

    public void SetProductionJob(int index, FixedString64Bytes id)
    {
        buildingId = default;
        jobId = id;
        commandType = CommandType.Progression;
        dataIndex = index;
    }
}
