using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Text;

public class CommandMenu : MonoBehaviour
{
    [SerializeField] private GameObject ButtonPrefab;
    [SerializeField] private GameDataRegistry presentationRegistry;
    [SerializeField] private RectTransform selectionPanelRoot;
    [SerializeField] private float infoWidth = 280f;
    [SerializeField] private float minimumHeight = 144f;
    [SerializeField] private float panelGap = 8f;
    [SerializeField] private int maximumColumns = 4;
    [SerializeField] private CommandTooltipView tooltip;
    [SerializeField, Min(0.02f)] private float stateRefreshInterval = 0.1f;
    [Header("Presentation states")]
    [SerializeField] private Color disabledIconTint = new Color(0.62f, 0.65f, 0.63f, 1f);
    [SerializeField] private Color blockedBadgeColor = new Color(1f, 0.76f, 0.38f, 1f);
    [SerializeField] private Color completedBadgeColor = new Color(0.55f, 0.87f, 0.65f, 1f);
    [SerializeField] private string blockedBadgeText = "!";
    [SerializeField] private string completedBadgeText = "OK";

    private sealed class ButtonEntry
    {
        public bool IsBuild;
        public FixedString64Bytes BuildingId;
        public ProductionElement Offer;
        public Button Button;
        public Image Image;
        public Color OriginalColor;
        public CommandButton Command;
        public CommandHintTarget Hint;
        public string Header;
        public TextMeshProUGUI StatusBadge;
    }
    private readonly List<ButtonEntry> entries = new();
    private float nextStateRefresh;

    private Entity lastSelected;
    private World lastWorld;
    private GameDataRegistry lastRegistry;
    private int lastPlayerId = -1;

    private void Update()
    {
        bool hasPlayer = SelectHelper.TryGetLocalPlayerId(out int playerId);
        Entity selectedEntity = hasPlayer
            ? SelectHelper.GetFirstSelectedEntityByplayerID(playerId)
            : Entity.Null;
        if (ResourceInspection.TryGet(out _)) selectedEntity = Entity.Null;

        var world = World.DefaultGameObjectInjectionWorld;
        var registry = EntityPresentation.ResolveRegistry(presentationRegistry);
        if (selectedEntity == lastSelected && world == lastWorld && registry == lastRegistry && playerId == lastPlayerId &&
            OffersMatch(world, selectedEntity))
        {
            if (Time.unscaledTime >= nextStateRefresh) RefreshButtonStates();
            return;
        }

        lastSelected = selectedEntity;
        lastWorld = world;
        lastRegistry = registry;
        lastPlayerId = playerId;

        ClearButtons();

        if (selectedEntity == Entity.Null)
            return;

        if (world == null || !world.IsCreated)
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
        if (ButtonPrefab == null) return;

        if (hasBuildOffers)
        {
            var buildOffers = entityManager.GetBuffer<BuildOffer>(selectedEntity);
            for (int i = 0; i < buildOffers.Length; i++)
            {
                var offer = buildOffers[i];
                GameObject buttonObject = Instantiate(ButtonPrefab, transform);
                Image image = GetButtonImageComponent(buttonObject);

                Sprite icon = EntityPresentation.BuildingIcon(offer.DefinitionID, registry);
                ConfigureTile(buttonObject, image, icon, EntityPresentation.BuildingName(offer.DefinitionID, registry));

                CommandButton commandButton = buttonObject.GetComponent<CommandButton>();
                if (commandButton == null)
                    commandButton = buttonObject.AddComponent<CommandButton>();

                commandButton.SetBuildOffer(offer.DefinitionID, i);

                Button button = buttonObject.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                }
                RegisterButton(buttonObject, image, button, commandButton, true, offer.DefinitionID, default);
            }
        }

        if (hasProduction)
        {
            var productList = entityManager.GetBuffer<ProductionElement>(selectedEntity);
            for (int i = 0; i < productList.Length; i++)
            {
                var offer = productList[i];
                GameObject buttonObject = Instantiate(ButtonPrefab, transform);
                Image image = GetButtonImageComponent(buttonObject);
                ConfigureTile(buttonObject, image, EntityPresentation.JobIcon(offer, registry),
                    EntityPresentation.JobName(offer, registry));

                CommandButton commandButton = buttonObject.GetComponent<CommandButton>();
                if (commandButton == null)
                    commandButton = buttonObject.AddComponent<CommandButton>();

                commandButton.SetProductionJob(offer.JobReference);

                Button button = buttonObject.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                }
                RegisterButton(buttonObject, image, button, commandButton, false, default, offer);
            }
        }
        UpdatePanelLayout((hasBuildOffers ? entityManager.GetBuffer<BuildOffer>(selectedEntity).Length : 0) +
            (hasProduction ? entityManager.GetBuffer<ProductionElement>(selectedEntity).Length : 0));
        RefreshButtonStates();
    }

    private static void ConfigureTile(GameObject tile, Image image, Sprite icon, string title)
    {
        if (image != null)
        {
            image.sprite = icon;
            image.enabled = icon != null;
            image.preserveAspect = true;
        }
        var label = tile.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) label.text = title;
    }

    private void UpdatePanelLayout(int count)
    {
        var grid = GetComponent<GridLayoutGroup>();
        var rect = transform as RectTransform;
        if (grid == null || rect == null) return;
        int columns = Mathf.Min(Mathf.Max(1, maximumColumns), Mathf.Max(1, count));
        int rows = count == 0 ? 0 : (count + columns - 1) / columns;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        float width = count == 0 ? 0f : columns * grid.cellSize.x + (columns - 1) * grid.spacing.x;
        float height = rows == 0 ? 0f : rows * grid.cellSize.y + (rows - 1) * grid.spacing.y;
        rect.sizeDelta = new Vector2(width, height);
        var background = GetComponent<Image>();
        if (background != null) background.enabled = count > 0;
        if (selectionPanelRoot != null)
            selectionPanelRoot.sizeDelta = new Vector2(infoWidth + (count > 0 ? panelGap + width : 0f),
                Mathf.Max(minimumHeight, height + 16f));
    }

    private void RegisterButton(GameObject tile, Image image, Button button, CommandButton command,
        bool isBuild, FixedString64Bytes buildingId, ProductionElement offer)
    {
        var label = tile.GetComponentInChildren<TextMeshProUGUI>(true);
        var hint = tile.GetComponent<CommandHintTarget>() ?? tile.AddComponent<CommandHintTarget>();
        hint.Initialize(tooltip);
        var entry = new ButtonEntry
        {
            IsBuild = isBuild, BuildingId = buildingId, Offer = offer,
            Button = button, Image = image, OriginalColor = image != null ? image.color : Color.white,
            Command = command, Hint = hint,
            StatusBadge = tile.transform.Find("StatusBadge")?.GetComponent<TextMeshProUGUI>(),
            Header = (isBuild ? "Build " : offer.Kind == ProductionKind.Train ? "Train " : "Research ") +
                (label != null ? label.text : string.Empty)
        };
        entries.Add(entry);
        if (button != null) button.onClick.AddListener(() => TrySubmit(entry));
    }

    private bool OffersMatch(World world, Entity entity)
    {
        if (world == null || !world.IsCreated || entity == Entity.Null) return entries.Count == 0;
        var em = world.EntityManager;
        if (!em.Exists(entity)) return false;
        int buildCount = em.HasBuffer<BuildOffer>(entity) ? em.GetBuffer<BuildOffer>(entity, true).Length : 0;
        int productionCount = em.HasBuffer<ProductionElement>(entity) ? em.GetBuffer<ProductionElement>(entity, true).Length : 0;
        if (entries.Count != buildCount + productionCount) return false;
        for (int i = 0; i < buildCount; i++)
            if (!entries[i].IsBuild || entries[i].BuildingId != em.GetBuffer<BuildOffer>(entity, true)[i].DefinitionID) return false;
        for (int i = 0; i < productionCount; i++)
        {
            var candidate = em.GetBuffer<ProductionElement>(entity, true)[i];
            var entry = entries[buildCount + i];
            if (entry.IsBuild || entry.Offer.JobID != candidate.JobID || entry.Offer.Kind != candidate.Kind ||
                entry.Offer.JobReference.CatalogKey != candidate.JobReference.CatalogKey ||
                entry.Offer.JobReference.ID != candidate.JobReference.ID ||
                entry.Offer.UnitPrefab != candidate.UnitPrefab) return false;
        }
        return true;
    }

    private void RefreshButtonStates()
    {
        nextStateRefresh = Time.unscaledTime + Mathf.Max(0.02f, stateRefreshInterval);
        foreach (var entry in entries) RefreshEntry(entry);
    }

    private bool RefreshEntry(ButtonEntry entry)
    {
        var status = lastWorld != null && lastWorld.IsCreated
            ? CommandAvailabilityReader.Read(lastWorld.EntityManager, lastSelected, lastPlayerId,
                entry.IsBuild, entry.BuildingId, entry.Offer)
            : new CommandAvailability(false, "Waiting for game data");
        if (entry.Button != null) entry.Button.interactable = status.Allowed;
        if (entry.Image != null)
            entry.Image.color = status.Allowed || status.Completed ? entry.OriginalColor : disabledIconTint;
        if (entry.StatusBadge != null)
        {
            entry.StatusBadge.enabled = !status.Allowed;
            entry.StatusBadge.text = status.Completed ? completedBadgeText : blockedBadgeText;
            entry.StatusBadge.color = status.Completed ? completedBadgeColor : blockedBadgeColor;
        }
        var text = new StringBuilder(entry.Header).AppendLine();
        if (!string.IsNullOrEmpty(status.Costs)) text.AppendLine(status.Costs);
        text.Append(status.Reason);
        if (!string.IsNullOrEmpty(status.Detail)) text.AppendLine().Append(status.Detail);
        entry.Hint.SetContent(text.ToString());
        return status.Allowed;
    }

    private void TrySubmit(ButtonEntry entry)
    {
        if (ResourceInspection.TryGet(out _)) return;
        // Recheck at click time; a resource/unlock/selection change may happen between refreshes.
        if (World.DefaultGameObjectInjectionWorld != lastWorld ||
            !SelectHelper.TryGetLocalPlayerId(out int playerId) || playerId != lastPlayerId ||
            SelectHelper.GetFirstSelectedEntityByplayerID(playerId) != lastSelected) return;
        if (!OffersMatch(lastWorld, lastSelected)) return;
        if (RefreshEntry(entry) && entry.Command != null) entry.Command.OnClick();
    }

    private void ClearButtons()
    {
        entries.Clear();
        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(false); // Remove from layout before deferred destruction.
            Destroy(child.gameObject);
        }
        UpdatePanelLayout(0);
    }

    private void OnEnable()
    {
        lastWorld = null;
        lastRegistry = null;
        lastPlayerId = -1;
    }

    private void OnDisable() => ClearButtons();

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
