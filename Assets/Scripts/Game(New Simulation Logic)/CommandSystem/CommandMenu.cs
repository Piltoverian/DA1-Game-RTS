using System;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

public class CommandMenu : MonoBehaviour
{
    [SerializeField] private GameObject ButtonPrefab;

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

        bool hasBuildOffers = entityManager.HasBuffer<BuildOffer>(selectedEntity);
        bool hasProduction = entityManager.HasBuffer<ProductionElement>(selectedEntity);
        bool hasCommands = entityManager.HasBuffer<CommandElement>(selectedEntity);

        if (!hasBuildOffers && !hasProduction && !hasCommands)
            return;

        if (hasBuildOffers)
        {
            var buildOffers = entityManager.GetBuffer<BuildOffer>(selectedEntity);
            for (int i = 0; i < buildOffers.Length; i++)
            {
                var offer = buildOffers[i];
                GameObject buttonObject = Instantiate(ButtonPrefab, transform);
                Image image = GetButtonImageComponent(buttonObject);

                Sprite icon = EntityPresentation.BuildingIcon(offer.DefinitionID);
                if (icon != null && image != null)
                {
                    image.sprite = icon;
                }
                else if (image != null)
                {
                    var iconMapping = Resources.Load<IconMapping>("IconMapping");
                    if (iconMapping != null)
                    {
                        string bName = EntityPresentation.BuildingName(offer.DefinitionID);
                        image.sprite = iconMapping.GetIconOfCommand(bName);
                    }
                }

                CommandButton commandButton = buttonObject.GetComponent<CommandButton>();
                if (commandButton == null)
                    commandButton = buttonObject.AddComponent<CommandButton>();

                commandButton.SetBuildOffer(offer.DefinitionID, i);

                Button button = buttonObject.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(commandButton.OnClick);
                }
            }
        }

        if (hasProduction && !hasCommands)
        {
            var productList = entityManager.GetBuffer<ProductionElement>(selectedEntity);
            for (int i = 0; i < productList.Length; i++)
            {
                var offer = productList[i];
                GameObject buttonObject = Instantiate(ButtonPrefab, transform);
                Image image = GetButtonImageComponent(buttonObject);
                if (image != null)
                    image.sprite = EntityPresentation.JobIcon(offer);

                CommandButton commandButton = buttonObject.GetComponent<CommandButton>();
                if (commandButton == null)
                    commandButton = buttonObject.AddComponent<CommandButton>();

                commandButton.SetCommandDataFromCommandData(new CommandData
                {
                    Type = CommandType.Progression,
                    indexInUnitCommandList = i
                });

                Button button = buttonObject.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(commandButton.OnClick);
                }
                ApplyProductionUnlockState(entityManager, GameManager.Instance.GetModule<SelectManager>().currentContext.playerId, offer, button, image);
            }
        }

        if (hasCommands)
        {
            NativeList<CommandElement> commands =
                CommandDataHelper.GetCommandsForEntity(entityManager, selectedEntity);

            foreach (CommandElement command in commands)
            {
                if (hasBuildOffers && command.Type == CommandType.Build)
                    continue;

                GameObject buttonObject = Instantiate(ButtonPrefab, transform);
                Image image = GetButtonImageComponent(buttonObject);

                if (command.Type == CommandType.Build)
                {
                    var iconMapping = Resources.Load<IconMapping>("IconMapping");
                    if (iconMapping != null && image != null)
                        image.sprite = iconMapping.GetIconOfCommand("Build");
                }
                else if (command.Type == CommandType.Progression)
                {
                    if (entityManager.HasBuffer<ProductionElement>(selectedEntity))
                    {
                        var productList = entityManager.GetBuffer<ProductionElement>(selectedEntity);
                        if (command.indexInUnitCommandList >= 0 && command.indexInUnitCommandList < productList.Length)
                        {
                            if (image != null)
                                image.sprite = EntityPresentation.JobIcon(productList[command.indexInUnitCommandList]);
                        }
                    }
                }

                CommandButton commandButton = buttonObject.GetComponent<CommandButton>();
                if (commandButton == null)
                    commandButton = buttonObject.AddComponent<CommandButton>();

                commandButton.SetCommandDataFromBufferElement(command);

                Button button = buttonObject.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(commandButton.OnClick);
                }

                if (command.Type == CommandType.Progression && entityManager.HasBuffer<ProductionElement>(selectedEntity))
                {
                    var productList = entityManager.GetBuffer<ProductionElement>(selectedEntity);
                    if (command.indexInUnitCommandList >= 0 && command.indexInUnitCommandList < productList.Length)
                    {
                        ApplyProductionUnlockState(entityManager, GameManager.Instance.GetModule<SelectManager>().currentContext.playerId, productList[command.indexInUnitCommandList], button, image);
                    }
                }
            }

            commands.Dispose();
        }
    }

    private void ApplyProductionUnlockState(EntityManager entityManager, int playerId, ProductionElement offer, Button button, Image image)
    {
        if (button == null) return;
        if (!ProductionJobs.TryRegistry(entityManager, out var registryEntity)) return;
        var registry = entityManager.GetBuffer<RegistryBlobElement>(registryEntity, true);
        if (offer.Kind == ProductionKind.Train)
        {
            var job = registry.GetBlobByID<TrainBlob>(offer.JobID, out var found);
            if (found == FunctionResult.Success)
            {
                var status = TechUnlockHelper.CheckUnitUnlockStatus(entityManager, playerId, job.Value.OutputUnitID);
                if (status != UnitUnlockStatus.Available)
                {
                    button.interactable = false;
                    if (image != null) image.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                }
            }
        }
        else if (offer.Kind == ProductionKind.Research)
        {
            var job = registry.GetBlobByID<ResearchBlob>(offer.JobID, out var found);
            if (found == FunctionResult.Success)
            {
                var status = TechUnlockHelper.CheckTechUnlockStatus(entityManager, playerId, job.Value.Tech.ID);
                if (status == TechUnlockStatus.Completed)
                {
                    button.interactable = false;
                    if (image != null) image.color = new Color(0.3f, 0.7f, 0.3f, 0.6f);
                }
                else if (status != TechUnlockStatus.Available)
                {
                    button.interactable = false;
                    if (image != null) image.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                }
            }
        }
    }
    private void ClearButtons()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
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
        return null;
    }
}


