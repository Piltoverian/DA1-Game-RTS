using System;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

public class CommandMenu : MonoBehaviour
{
    [SerializeField] private GameObject ButtonPrefab;
    [SerializeField] private BuildingPlacementDatabase buildingPlacementDatabase;

    private Entity lastSelected;

    private void Update()
    {
        Entity selectedEntity =
            SelectHelper.GetFirstSelectedEntityByplayerID(
                GameManager.Instance.GetModule<SelectManager>().currentContext.playerId
            );

        if (selectedEntity == lastSelected)
            return;

        lastSelected = selectedEntity;

        ClearButtons();

        if (selectedEntity == Entity.Null)
            return;

        var world = World.DefaultGameObjectInjectionWorld;

        if (world == null)
        {
            Debug.LogError("DefaultGameObjectInjectionWorld is null.");
            return;
        }

        EntityManager entityManager = world.EntityManager;

        if (!entityManager.Exists(selectedEntity))
            return;

        if (!entityManager.HasBuffer<CommandElement>(selectedEntity))
        {
            Debug.Log($"Selected entity {selectedEntity} has no CommandElement buffer.");
            return;
        }

        NativeList<CommandElement> commands =
            CommandDataHelper.GetCommandsForEntity(entityManager, selectedEntity);

        foreach (CommandElement command in commands)
        {
            GameObject buttonObject = Instantiate(ButtonPrefab, transform);

            //TMP_Text text = buttonObject.GetComponentInChildren<TMP_Text>();

            //if (text != null)
            //{
            //    text.text = GetCommandLabel(command);
            //}
            Image image = GetButtonImageComponent(buttonObject);
            var IconMapping= Resources.Load<IconMapping>("IconMapping");
            if (IconMapping == null)
            {
                Debug.LogWarning("IconMapping asset not found in Resources folder.");
                continue;
            }
            else
            {
                if (command.Type== CommandType.Build)
                {
                    var buildingdef = buildingPlacementDatabase.GetByCommandIndex(command.indexInUnitCommandList);
                    var buildingName = buildingdef != null ? buildingdef.DisplayName : "Unknown Building";

                    image.sprite = IconMapping.GetIconOfCommand(buildingName);
                }

                if (command.Type == CommandType.Progression)
                {
                   
                    var productlist=entityManager.GetBuffer<ProductionElement>(selectedEntity);
                    var commandprefab= productlist[command.indexInUnitCommandList].UnitPrefab;
                    if (entityManager.HasComponent<Unit>(commandprefab))
                    {
                        var unitName = entityManager.GetComponentData<Unit>(commandprefab);
                        Debug.Log($"Setting icon for Progression command with index {command.indexInUnitCommandList}." + "UnitName: " + unitName.GetValueNormalizedString());
                        if (image == null)
                        {
                            Debug.LogWarning("ButtonPrefab does not have an Image component.");
                        }
                        if (IconMapping.GetIconOfCommand(unitName.GetValueNormalizedString()) != null)
                        {
                            image.sprite = IconMapping.GetIconOfCommand(unitName.GetValueNormalizedString());
                            Debug.Log($"Icon set for unit {unitName.GetValueNormalizedString()}.");
                        }
                        else
                        {
                            Debug.LogWarning($"No icon found for unit {unitName.GetValueNormalizedString()} in CommandIconMapping.");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Command prefab {commandprefab} does not have UnitName component.");
                    }
                }
            }
            CommandButton commandButton = buttonObject.GetComponent<CommandButton>();

            if (commandButton == null)
            {
                commandButton = buttonObject.AddComponent<CommandButton>();
            }

            commandButton.SetCommandDataFromBufferElement(command);

            Button button = buttonObject.GetComponent<Button>();

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(commandButton.OnClick);
            }
            else
            {
                Debug.LogWarning("ButtonPrefab does not have UnityEngine.UI.Button component.");
            }
        }

        Debug.Log($"Entity {selectedEntity} selected, displaying {commands.Length} commands.");

        commands.Dispose();
    }
    private void ClearButtons()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
    }

    private string GetCommandLabel(CommandElement command)
    {
        if (command.Type == CommandType.Build)
        {
            if (buildingPlacementDatabase != null)
            {
                BuildingPlacementDefinition definition =
                    buildingPlacementDatabase.GetByCommandIndex(command.indexInUnitCommandList);

                if (definition != null && !string.IsNullOrEmpty(definition.DisplayName))
                {
                    return definition.DisplayName;
                }
            }

            return "Build " + command.indexInUnitCommandList;
        }

        if (command.Type == CommandType.Progression)
        {
            return "Train Unit " + command.indexInUnitCommandList;
        }

        return Enum.GetName(typeof(CommandType), command.Type);
    }

    Image GetButtonImageComponent(GameObject buttonObject)
    {
        Transform buttonTransform = buttonObject.transform;

        foreach (Transform child in buttonTransform)
        {
            Image image = child.GetComponent<Image>();
            if (image != null)
            {
                return image;
            }
        }
        Debug.LogWarning("ButtonPrefab does not have an Image component.");
        return null;
    }
}
