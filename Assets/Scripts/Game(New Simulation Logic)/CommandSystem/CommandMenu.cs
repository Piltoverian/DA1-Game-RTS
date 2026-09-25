using System;
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

        if (!hasBuildOffers && !hasProduction)
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

        if (hasProduction)
        {
            var productList = entityManager.GetBuffer<ProductionElement>(selectedEntity);
            int playerId = GameManager.Instance.GetModule<SelectManager>().currentContext.playerId;
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

                commandButton.SetProductionJob(i, offer.JobID);

                Button button = buttonObject.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(commandButton.OnClick);
                }
                ApplyProductionUnlockState(entityManager, playerId, offer, button, image);
            }
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
