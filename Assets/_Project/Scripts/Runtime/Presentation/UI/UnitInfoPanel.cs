using System.Globalization;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UnitInfoPanel : MonoBehaviour
{
    // Preserve the prototype's serialized reference; views are now authored in UICanvas.
    [SerializeField, HideInInspector] private InfoPanel InfoPanelPrefab;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private TextMeshProUGUI contextText;
    [SerializeField] private Image constructionTrack;
    [SerializeField] private Image constructionFill;
    [SerializeField] private UnitImage portrait;
    [SerializeField] private GameDataRegistry presentationRegistry;
    [SerializeField] private Image healthFill;
    [SerializeField] private Image healthTrack;
    [SerializeField] private Image panelBackground;
    [SerializeField] private Image infoBackground;
    [SerializeField] private HudFrameGraphic infoFrame;
    [SerializeField] private HudFrameGraphic portraitFrame;
    [SerializeField] private Outline panelOutline;
    [SerializeField] private CommandHintTarget infoHint;
    [SerializeField] private CommandTooltipView tooltip;
    [Header("Health colors (numeric HP remains visible)")]
    [SerializeField] private Color healthyColor = new Color(0.32f, 0.75f, 0.47f, 1f);
    [SerializeField] private Color warningColor = new Color(0.94f, 0.68f, 0.3f, 1f);
    [SerializeField] private Color criticalColor = new Color(0.95f, 0.4f, 0.35f, 1f);

    [Header("Text (ready for localization binding)")]
    [SerializeField] private string waitingText = "Waiting for player...";
    [SerializeField] private string noSelectionText = "No selection";
    [SerializeField] private string unknownName = "Unknown object";
    [SerializeField] private string healthFormat = "HP: {0:0}/{1:0}";
    [SerializeField] private string groupHealthFormat = "Rep. HP: {0:0}/{1:0}";
    [SerializeField] private string missingHealthText = "HP: --/--";
    [SerializeField] private string countFormat = "Selected: {0}";
    [Header("Context text (representative only for groups)")]
    [SerializeField] private string representativeFormat = "Rep.: {0}";
    [SerializeField] private string noTaskText = "No work task";
    [SerializeField] private string toResourceText = "To resource";
    [SerializeField] private string gatheringText = "Gathering";
    [SerializeField] private string returningText = "Returning";
    [SerializeField] private string toBuildText = "To build site";
    [SerializeField] private string buildingText = "Building";
    [SerializeField] private string cargoFormat = "{0}: {1}/{2}";
    [SerializeField] private string emptyCargoText = "Cargo: 0";
    [SerializeField] private string plannedText = "Planned";
    [SerializeField] private string completeText = "Completed";
    [SerializeField] private string destroyedText = "Destroyed";
    [SerializeField] private string progressFormat = "{0}: {1}%";
    [SerializeField] private string unknownProgressFormat = "{0}: --%";
    [Header("Resource inspection")]
    [SerializeField] private Sprite goldResourceIcon;
    [SerializeField] private Sprite woodResourceIcon;
    [SerializeField] private Sprite foodResourceIcon;
    [SerializeField] private string resourceNameFormat = "{0} deposit";
    [SerializeField] private string remainingFormat = "Remaining: {0:N0}";
    [SerializeField] private string tierFormat = "Tier: {0}";
    [SerializeField] private string gatherHint = "Worker: right-click";
    private float nextContextRefresh;

    private World world;
    private EntityQuery selectedQuery;
    private Entity displayedEntity;
    private FixedString64Bytes displayedDefinition;
    private GameDataRegistry displayedRegistry;
    private float displayedHealth = float.NaN;
    private float displayedMaxHealth = float.NaN;
    private bool displayedGroup;
    private int displayedCount = -1;

    private void Awake() { if (infoHint != null) infoHint.Initialize(tooltip); }

    private void OnEnable() => ShowEmpty(waitingText);

    private void Update()
    {
        var currentWorld = World.DefaultGameObjectInjectionWorld;
        if (currentWorld != world)
        {
            ReleaseQuery();
            ShowEmpty(waitingText);
            if (currentWorld != null && currentWorld.IsCreated)
            {
                world = currentWorld;
                // Same query and filtering/order as SelectHelper, also used by CommandMenu.
                selectedQuery = world.EntityManager.CreateEntityQuery(typeof(Selected));
            }
        }

        if (world == null || !world.IsCreated || !SelectHelper.TryGetLocalPlayerId(out int playerId))
        {
            ShowEmpty(waitingText);
            return;
        }

        var manager = world.EntityManager;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame &&
            !GameplayInputGate.IsEditingText && !BuildingPlacer.IsPlacementActive) ResourceInspection.Clear();
        if (ResourceInspection.TryGet(out var resource))
        {
            ShowResource(manager, resource);
            return;
        }
        Entity representative = Entity.Null;
        int count = 0;
        using (var selected = selectedQuery.ToEntityArray(Allocator.Temp))
        {
            foreach (var entity in selected)
            {
                if (!manager.Exists(entity) || !manager.HasComponent<Selectable>(entity) ||
                    manager.GetComponentData<Selectable>(entity).playerID != playerId) continue;
                if (representative == Entity.Null) representative = entity;
                count++;
            }
        }

        if (representative == Entity.Null)
        {
            ClearInfo();
            return;
        }

        FixedString64Bytes definition = default;
        if (manager.HasComponent<UnitComponent>(representative))
            definition = manager.GetComponentData<UnitComponent>(representative).DefinitionID;
        else if (manager.HasComponent<BuildingComponent>(representative))
            definition = manager.GetComponentData<BuildingComponent>(representative).DefinitionID;

        SetVisible(true);
        var registry = EntityPresentation.ResolveRegistry(presentationRegistry);
        if (representative != displayedEntity || !definition.Equals(displayedDefinition) || registry != displayedRegistry)
        {
            nextContextRefresh = 0f;
            displayedEntity = representative;
            displayedDefinition = definition;
            displayedRegistry = registry;
            string title = definition.IsEmpty ? unknownName : EntityPresentation.GetName(definition, registry);
            SetText(nameText, string.IsNullOrWhiteSpace(title) ? unknownName : title);
            if (portrait != null) portrait.SetPortrait(EntityPresentation.Icon(manager, representative, registry), true);
            displayedHealth = float.NaN;
            displayedMaxHealth = float.NaN;
        }

        bool isGroup = count > 1;
        if (count != displayedCount)
        {
            nextContextRefresh = 0f;
            displayedCount = count;
            SetText(countText, isGroup ? string.Format(CultureInfo.CurrentCulture, countFormat, count) : string.Empty);
        }

        if (Time.unscaledTime >= nextContextRefresh)
        {
            nextContextRefresh = Time.unscaledTime + 0.1f;
            RefreshContext(manager, representative, isGroup);
        }

        if (manager.HasComponent<EntityHealth>(representative))
        {
            var health = manager.GetComponentData<EntityHealth>(representative);
            if (IsFinite(health.CurrentHP) && IsFinite(health.MaxHP) && health.MaxHP > 0f)
            {
                if (healthTrack != null) healthTrack.enabled = true;
                if (healthFill != null)
                {
                    healthFill.enabled = true;
                    healthFill.fillAmount = Mathf.Clamp01(health.CurrentHP / health.MaxHP);
                    healthFill.color = healthFill.fillAmount <= 0.25f ? criticalColor :
                        healthFill.fillAmount <= 0.5f ? warningColor : healthyColor;
                }
                float current = Mathf.Round(Mathf.Max(0f, health.CurrentHP));
                float maximum = Mathf.Round(health.MaxHP);
                if (current != displayedHealth || maximum != displayedMaxHealth || isGroup != displayedGroup)
                {
                    SetText(healthText, string.Format(CultureInfo.CurrentCulture,
                        isGroup ? groupHealthFormat : healthFormat, current, maximum));
                    displayedHealth = current;
                    displayedMaxHealth = maximum;
                }
                displayedGroup = isGroup;
                return;
            }
        }

        SetText(healthText, missingHealthText);
        HideHealthBar();
        displayedHealth = float.NaN;
        displayedMaxHealth = float.NaN;
        displayedGroup = isGroup;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private void ShowResource(EntityManager manager, Entity entity)
    {
        SetVisible(true);
        HideHealthBar();
        SetConstructionBar(false, 0f);
        SetText(countText, string.Empty);
        if (entity != displayedEntity)
        {
            displayedEntity = entity;
            displayedDefinition = default;
            displayedRegistry = null;
            displayedCount = -1;
            displayedHealth = displayedMaxHealth = float.NaN;
            nextContextRefresh = 0f;
        }
        if (Time.unscaledTime < nextContextRefresh) return;
        nextContextRefresh = Time.unscaledTime + 0.1f;
        var node = manager.GetComponentData<ResourceNodeData>(entity);
        SetText(nameText, string.Format(CultureInfo.CurrentCulture, resourceNameFormat, node.Type));
        SetText(healthText, string.Format(CultureInfo.CurrentCulture, remainingFormat, node.Amount));
        string tier = manager.HasComponent<MapResourceIdentity>(entity)
            ? string.Format(CultureInfo.CurrentCulture, tierFormat, manager.GetComponentData<MapResourceIdentity>(entity).Tier)
            : string.Empty;
        SetText(contextText, string.IsNullOrEmpty(tier) ? gatherHint : tier + "\n" + gatherHint);
        if (infoHint != null) infoHint.SetContent((nameText != null ? nameText.text : string.Empty) + "\n" +
            (healthText != null ? healthText.text : string.Empty) + "\n" + tier +
            "\nSelect workers, then right-click a deposit to gather.\nYour previous unit selection is retained.");
        if (portrait != null) portrait.SetPortrait(node.Type switch
        {
            ResourceType.Gold => goldResourceIcon,
            ResourceType.Wood => woodResourceIcon,
            ResourceType.Food => foodResourceIcon,
            _ => null
        }, true);
    }

    private void RefreshContext(EntityManager manager, Entity entity, bool isGroup)
    {
        SetConstructionBar(false, 0f);
        string text = string.Empty;
        if (manager.HasComponent<BuildingConstruction>(entity))
        {
            var construction = manager.GetComponentData<BuildingConstruction>(entity);
            string phase = construction.Phase switch
            {
                ConstructionPhase.Planned => plannedText,
                ConstructionPhase.UnderConstruction => buildingText,
                ConstructionPhase.Completed => completeText,
                ConstructionPhase.Destroyed => destroyedText,
                _ => string.Empty
            };
            text = phase;
            if (construction.Phase == ConstructionPhase.Planned || construction.Phase == ConstructionPhase.UnderConstruction)
            {
                float workload = manager.HasComponent<BuildingComponent>(entity)
                    ? manager.GetComponentData<BuildingComponent>(entity).WorkLoad : 0f;
                bool valid = IsFinite(workload) && workload > 0f && IsFinite(construction.CompletedWork);
                if (valid)
                {
                    float fraction = Mathf.Clamp01(construction.CompletedWork / workload);
                    // Completion is authoritative; never label an unfinished building as 100%.
                    int percent = Mathf.Min(99, Mathf.FloorToInt(fraction * 100f));
                    text = string.Format(CultureInfo.CurrentCulture, progressFormat, phase, percent);
                    SetConstructionBar(true, fraction);
                }
                else text = string.Format(CultureInfo.CurrentCulture, unknownProgressFormat, phase);
            }
        }
        else if (manager.HasComponent<WorkerTag>(entity))
        {
            string activity = noTaskText;
            bool hasGather = manager.HasComponent<WorkerGatherData>(entity);
            WorkerGatherData gather = hasGather ? manager.GetComponentData<WorkerGatherData>(entity) : default;
            // Disabled components retain their last state/cargo. Only enabled tasks describe activity.
            if (hasGather && manager.IsComponentEnabled<WorkerGatherData>(entity))
                activity = gather.State switch
                {
                    WorkerGatherState.GoingToNode => toResourceText,
                    WorkerGatherState.Gathering => gatheringText,
                    WorkerGatherState.ReturningDepot => returningText,
                    _ => noTaskText
                };
            if (manager.HasComponent<BuilderComponent>(entity) && manager.IsComponentEnabled<BuilderComponent>(entity))
            {
                var builder = manager.GetComponentData<BuilderComponent>(entity);
                if (builder.State == BuilderState.GoingToSite) activity = toBuildText;
                else if (builder.State == BuilderState.Building) activity = buildingText;
            }
            text = activity;
            if (hasGather)
            {
                string cargo = gather.CarryAmount > 0
                    ? string.Format(CultureInfo.CurrentCulture, cargoFormat, gather.CurrentResourceType,
                        gather.CarryAmount, gather.Capacity > 0 ? gather.Capacity.ToString(CultureInfo.CurrentCulture) : "--")
                    : emptyCargoText;
                text += "\n" + cargo;
            }
        }
        if (isGroup && !string.IsNullOrEmpty(text))
            text = string.Format(CultureInfo.CurrentCulture, representativeFormat, text);
        string stats = SelectionStats.Read(manager, entity, out var summary);
        if (!string.IsNullOrEmpty(summary))
        {
            if (string.IsNullOrEmpty(text)) text = isGroup ? string.Format(CultureInfo.CurrentCulture, representativeFormat, summary) : summary;
            else if (manager.HasComponent<BuildingConstruction>(entity) &&
                manager.GetComponentData<BuildingConstruction>(entity).Phase == ConstructionPhase.Completed) text += "\n" + summary;
        }
        SetText(contextText, text);
        if (infoHint != null) infoHint.SetContent((nameText != null ? nameText.text : unknownName) + "\n" +
            (isGroup ? "Representative details only\n" : string.Empty) + text + "\n" + stats);
    }

    private void SetConstructionBar(bool visible, float fraction)
    {
        if (constructionTrack != null) constructionTrack.enabled = visible;
        if (constructionFill != null)
        {
            constructionFill.enabled = visible;
            constructionFill.fillAmount = fraction;
        }
    }

    private static void SetText(TextMeshProUGUI target, string value)
    {
        if (target != null && target.text != value) target.text = value;
    }

    private void ShowEmpty(string message)
    {
        displayedEntity = Entity.Null;
        displayedDefinition = default;
        displayedRegistry = null;
        displayedHealth = float.NaN;
        displayedMaxHealth = float.NaN;
        displayedCount = -1;
        displayedGroup = false;
        SetText(nameText, message);
        SetText(healthText, string.Empty);
        SetText(countText, string.Empty);
        SetText(contextText, string.Empty);
        if (infoHint != null) infoHint.Clear();
        SetConstructionBar(false, 0f);
        nextContextRefresh = 0f;
        if (portrait != null) portrait.SetPortrait(null, false);
        HideHealthBar();
        SetVisible(false);
    }

    private void HideHealthBar()
    {
        if (healthFill != null) healthFill.enabled = false;
        if (healthTrack != null) healthTrack.enabled = false;
    }

    private void SetVisible(bool visible)
    {
        // Draw only occupied cards; the outer rect is a layout container, not a large backdrop.
        if (panelBackground != null) panelBackground.enabled = false;
        if (infoBackground != null) infoBackground.enabled = visible;
        if (infoFrame != null) infoFrame.enabled = visible;
        if (portraitFrame != null) portraitFrame.enabled = visible;
        if (panelOutline != null) panelOutline.enabled = false;
        if (nameText != null) nameText.enabled = visible;
        if (healthText != null) healthText.enabled = visible;
        if (countText != null) countText.enabled = visible;
        if (contextText != null) contextText.enabled = visible;
    }

    public void ClearInfo() => ShowEmpty(noSelectionText);

    private void ReleaseQuery()
    {
        if (world != null && world.IsCreated) selectedQuery.Dispose();
        world = null;
    }

    private void OnDisable()
    {
        ResourceInspection.Clear();
        ReleaseQuery();
        ClearInfo();
    }
}
