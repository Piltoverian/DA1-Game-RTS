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

            TMP_Text text = buttonObject.GetComponentInChildren<TMP_Text>();

            if (text != null)
            {
                text.text = GetCommandLabel(command);
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
}
