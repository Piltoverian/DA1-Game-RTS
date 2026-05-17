using System;
using UnityEngine;
using Unity.Entities;
using UnityEngine.UI;
public class CommandMenu : MonoBehaviour
{

    [SerializeField] private GameObject ButtonPrefab;
    private Entity lastSelected;
    // Update is called once per frame
    void Update()
    {
        var selectedEntity = SelectHelper.GetFirstSelectedEntity();
        if (selectedEntity != lastSelected)
        {
            lastSelected = selectedEntity;
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }
            if (selectedEntity != Entity.Null)
            {
                var commands = CommandDataHelper.GetCommandsForEntity(World.DefaultGameObjectInjectionWorld.EntityManager, selectedEntity);
                foreach (var command in commands)
                {
                    var button = Instantiate(ButtonPrefab, transform);
                    button.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = Enum.GetName(typeof(CommandType), command.Type);
                    button.AddComponent<CommandButton>();
                    button.GetComponent<CommandButton>().SetCommandDataFromBufferElement(command);
                    button.GetComponent<Button>().onClick.AddListener(button.GetComponent<CommandButton>().OnClick);
                }
                Debug.Log($"Entity {selectedEntity} selected, displaying {commands.Length} commands.");
            }
        }
    }
}
