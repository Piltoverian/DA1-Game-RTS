using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Serialization;

public class CommandButton : MonoBehaviour
{
    [SerializeField] private CommandType commandType;
    [FormerlySerializedAs("dataIndex")]
    [SerializeField] private int buildOfferIndex;
    private FixedString64Bytes buildingId;
    private GameBlobReference progression;

    public void OnClick()
    {
        if (!SelectHelper.TryGetLocalPlayerId(out int playerId)) return;
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
                        progression: progression
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
                        BuildingPlacer.Instance.StartPlacementFromOfferIndex(buildOfferIndex, sourceEntity, playerId);
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
        progression = default;
        commandType = CommandType.Build;
        buildOfferIndex = index;
    }

    public void SetProductionJob(GameBlobReference reference)
    {
        buildingId = default;
        progression = reference;
        commandType = CommandType.Progression;
    }
}
