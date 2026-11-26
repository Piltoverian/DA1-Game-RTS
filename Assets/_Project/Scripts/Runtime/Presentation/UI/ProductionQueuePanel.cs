using System.Globalization;
using System.Text;
using System.Collections.Generic;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays existing jobs. Does not enqueue, cancel, pay, or refund.</summary>
public class ProductionQueuePanel : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TextMeshProUGUI body;
    [SerializeField] private Image icon;
    [SerializeField] private Image progress;
    [SerializeField] private GameDataRegistry presentationRegistry;
    [SerializeField] private RectTransform selectionPanelRoot;
    [SerializeField] private bool embeddedInDock;
    [SerializeField] private CommandTooltipView tooltip;
    private sealed class Tile
    {
        public RectTransform Rect;
        public Image Background, Icon;
        public TextMeshProUGUI Name, Count;
        public CommandHintTarget Hint;
    }
    private struct Entry { public string Key, Name; public Sprite Icon; public int Count; }
    private readonly List<Tile> tiles = new();
    private readonly List<Entry> groups = new();
    [SerializeField] private string titleFormat = "Queue {0}/{1}";
    [SerializeField] private string pausedText = "Production paused";
    [SerializeField] private string populationText = "Ready - waiting for population";
    [SerializeField] private Color activeColor = new Color(.36f, .82f, .57f, 1f);
    [SerializeField] private Color waitingColor = new Color(1f, .76f, .38f, 1f);
    private float nextRefresh;
    private Entity previous;
    private World previousWorld;

    private void OnEnable() { nextRefresh = 0f; SetVisible(false); }
    private void LateUpdate()
    {
        if (embeddedInDock && body != null)
        {
            body.rectTransform.anchorMin = body.rectTransform.anchorMax = body.rectTransform.pivot = new Vector2(0, 1);
            body.rectTransform.anchoredPosition = new Vector2(8, -4);
            body.rectTransform.sizeDelta = new Vector2(((RectTransform)transform).rect.width - 16, 48);
        }
        // Menus can have multiple rows; queue always sits above the occupied cards.
        if (!embeddedInDock && selectionPanelRoot != null && transform is RectTransform rect)
            rect.anchoredPosition = selectionPanelRoot.anchoredPosition + new Vector2(0, selectionPanelRoot.rect.height + 12f);
    }
    private void Update()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        bool hasPlayer = SelectHelper.TryGetLocalPlayerId(out int player);
        Entity source = hasPlayer ? SelectHelper.GetFirstSelectedEntityByplayerID(player) : Entity.Null;
        if (ResourceInspection.TryGet(out _)) source = Entity.Null;
        bool changed = source != previous || world != previousWorld;
        previous = source; previousWorld = world;
        if (source == Entity.Null || world == null || !world.IsCreated) { SetVisible(false); return; }
        if (!changed && Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .1f;
        var em = world.EntityManager;
        if (em.Exists(source) && em.HasComponent<EntityHealth>(source) && em.GetComponentData<EntityHealth>(source).CurrentHP <= 0)
        { SetVisible(false); return; }
        if (embeddedInDock && em.Exists(source) && ShowConstruction(em, source)) return;
        if (!em.Exists(source) || !em.HasBuffer<ProductionQueueElement>(source) || !em.HasComponent<EntityWork>(source))
        { SetVisible(false); return; }
        if (em.HasComponent<EntityHealth>(source) && em.GetComponentData<EntityHealth>(source).CurrentHP <= 0)
        { SetVisible(false); return; }
        var queue = em.GetBuffer<ProductionQueueElement>(source, true);
        if (queue.Length == 0) { SetVisible(false); return; }
        SetVisible(true);
        var work = em.GetComponentData<EntityWork>(source);
        var first = queue[0];
        if (embeddedInDock)
        {
            groups.Clear();
            foreach (var item in queue)
                AddGroup(item.Kind + ":" + item.JobID,
                    EntityPresentation.JobName(item, presentationRegistry),
                    EntityPresentation.JobIcon(item, presentationRegistry));
        }
        var text = new StringBuilder(string.Format(CultureInfo.CurrentCulture, titleFormat, queue.Length, work.JobCapacity));
        text.Append(" - ").Append(EntityPresentation.JobName(first, presentationRegistry));
        float totalWork = 0f;
        if (ProductionJobs.TryRegistry(em, out var registryEntity))
        {
            var registry = em.GetBuffer<RegistryBlobElement>(registryEntity, true);
            if (first.Kind == ProductionKind.Train)
            {
                var job = registry.GetBlobByID<TrainBlob>(first.JobID, out var found);
                if (found == FunctionResult.Success) totalWork = job.Value.Job.WorkLoad;
            }
            else if (first.Kind == ProductionKind.Research)
            {
                var job = registry.GetBlobByID<ResearchBlob>(first.JobID, out var found);
                if (found == FunctionResult.Success) totalWork = job.Value.Job.WorkLoad;
            }
        }
        bool progressKnown = Finite(totalWork) && totalWork > 0 && Finite(first.RemainingWork);
        float fraction = progressKnown
            ? Mathf.Clamp01(1f - first.RemainingWork / totalWork) : 0f;
        bool populationBlocked = WaitingForPopulation(em, source, first, player);
        bool exitBlocked = em.HasComponent<ProductionData>(source) && em.GetComponentData<ProductionData>(source).ExitBlocked;
        text.AppendLine();
        bool construction = em.HasComponent<BuildingConstruction>(source) &&
            em.GetComponentData<BuildingConstruction>(source).Phase != ConstructionPhase.Completed;
        if (construction) text.Append("Under construction");
        else if (populationBlocked) text.Append(populationText);
        else if (exitBlocked) text.Append("Ready - exit blocked");
        else if (!progressKnown) text.Append("Progress unavailable");
        else if (first.RemainingWork <= 0) text.Append("Ready - finalizing");
        else if (!Finite(work.Rate) || work.Rate <= 0) text.Append(pausedText);
        else if (Finite(first.RemainingWork / work.Rate))
            text.AppendFormat(CultureInfo.CurrentCulture, "{0}% - current job ~{1:0.#}s", Mathf.Min(99, Mathf.FloorToInt(fraction * 100f)), Mathf.Max(0, first.RemainingWork / work.Rate));
        else text.Append("Time unavailable");
        if (queue.Length > 1 && !embeddedInDock)
        {
            text.AppendLine().Append("Waiting ").Append(queue.Length - 1).Append(": ");
            for (int i = 1; i < queue.Length && i <= 2; i++)
            {
                if (i > 1) text.Append(" / ");
                text.Append(EntityPresentation.JobName(queue[i], presentationRegistry));
            }
            if (queue.Length > 3) text.Append(" +").Append(queue.Length - 3);
        }
        if (progress != null)
        {
            progress.enabled = progressKnown;
            progress.fillAmount = fraction;
            progress.color = construction || populationBlocked || exitBlocked || !Finite(work.Rate) || work.Rate <= 0 ? waitingColor : activeColor;
        }
        string content = text.ToString();
        if (body != null && body.text != content) body.text = content;
        if (icon != null)
        {
            icon.sprite = EntityPresentation.JobIcon(first, presentationRegistry);
            icon.enabled = icon.sprite != null;
        }
        if (embeddedInDock) RenderTiles();
    }

    private bool ShowConstruction(EntityManager em, Entity selected)
    {
        Entity target = selected;
        bool builder = em.HasComponent<BuilderComponent>(selected) && em.IsComponentEnabled<BuilderComponent>(selected);
        if (builder) target = em.GetComponentData<BuilderComponent>(selected).TargetConstructionSite;
        groups.Clear();
        bool hasActive = AddSite(em, target);
        if (builder && em.HasBuffer<BuilderQueueElement>(selected))
            foreach (var pending in em.GetBuffer<BuilderQueueElement>(selected, true))
                if (pending.BuildingEntity != target) AddSite(em, pending.BuildingEntity);
        if (groups.Count == 0) return false;
        string status = "Waiting for builder";
        float fraction = 0;
        bool known = false;
        if (hasActive)
        {
            var construction = em.GetComponentData<BuildingConstruction>(target);
            float total = em.GetComponentData<BuildingComponent>(target).WorkLoad;
            known = Finite(total) && total > 0 && Finite(construction.CompletedWork);
            fraction = known ? Mathf.Clamp01(construction.CompletedWork / total) : 0;
            status = construction.Phase == ConstructionPhase.Planned ? "Planned" : "Under construction";
            if (builder && em.GetComponentData<BuilderComponent>(selected).State == BuilderState.GoingToSite)
                status = "Builder travelling";
        }
        body.text = "Construction - " + groups[0].Name + "\n" + status +
            (known ? $" - {Mathf.Min(99, Mathf.FloorToInt(fraction * 100))}%" : "");
        if (progress != null) { progress.enabled = known; progress.fillAmount = fraction; progress.color = activeColor; }
        SetVisible(true); RenderTiles(); return true;
    }
    private bool AddSite(EntityManager em, Entity site)
    {
        if (!em.Exists(site) || !em.HasComponent<BuildingConstruction>(site) || !em.HasComponent<BuildingComponent>(site)) return false;
        var phase = em.GetComponentData<BuildingConstruction>(site).Phase;
        if (phase == ConstructionPhase.Completed || phase == ConstructionPhase.Destroyed ||
            (em.HasComponent<EntityHealth>(site) && em.GetComponentData<EntityHealth>(site).CurrentHP <= 0)) return false;
        var id = em.GetComponentData<BuildingComponent>(site).DefinitionID;
        AddGroup("Build:" + id, EntityPresentation.BuildingName(id, presentationRegistry),
            EntityPresentation.BuildingIcon(id, presentationRegistry));
        return true;
    }
    // Consecutive runs retain FIFO order: A,A,B,A becomes A x2 / B / A.
    private void AddGroup(string key, string name, Sprite sprite)
    {
        if (groups.Count > 0 && groups[groups.Count - 1].Key == key)
        {
            var entry = groups[groups.Count - 1]; entry.Count++; groups[groups.Count - 1] = entry;
        }
        else groups.Add(new Entry { Key = key, Name = name, Icon = sprite, Count = 1 });
    }
    private void RenderTiles()
    {
        if (icon != null) icon.enabled = false;
        var rect = (RectTransform)transform;
        body.rectTransform.anchorMin = body.rectTransform.anchorMax = body.rectTransform.pivot = new Vector2(0, 1);
        body.rectTransform.anchoredPosition = new Vector2(8, -4);
        body.rectTransform.sizeDelta = new Vector2(rect.rect.width - 16, 48);
        body.fontSize = 14; body.enableAutoSizing = true; body.fontSizeMin = 12; body.fontSizeMax = 14;
        body.overflowMode = TextOverflowModes.Ellipsis;
        // Six pooled tiles; the last tile becomes a summary if more runs exist.
        int visible = Mathf.Min(6, groups.Count);
        while (tiles.Count < visible) tiles.Add(CreateTile());
        int columns = Mathf.Clamp(Mathf.FloorToInt((rect.rect.width - 10) / 82), 3, 6);
        float width = Mathf.Clamp((rect.rect.width - 16 - (columns - 1) * 6) / columns, 1, 96);
        for (int i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i]; tile.Rect.gameObject.SetActive(i < visible);
            if (i >= visible) continue;
            var entry = groups[i]; bool overflow = i == 5 && groups.Count > 6;
            tile.Rect.anchorMin = tile.Rect.anchorMax = tile.Rect.pivot = new Vector2(0, 1);
            tile.Rect.anchoredPosition = new Vector2(8 + (i % columns) * (width + 6), -58 - (i / columns) * 60);
            tile.Rect.sizeDelta = new Vector2(width, 54);
            tile.Background.color = i == 0 ? new Color(.14f, .31f, .26f, 1) : new Color(.07f, .13f, .12f, 1);
            tile.Icon.sprite = entry.Icon; tile.Icon.enabled = !overflow && entry.Icon != null;
            tile.Name.text = overflow ? $"+{groups.Count - 5} groups" : entry.Name;
            tile.Count.text = overflow ? "" : entry.Count.ToString();
            var hint = new StringBuilder(i == 0 ? "Current / first group\n" : "Waiting group\n");
            if (!overflow) hint.Append(entry.Name).Append(" x").Append(entry.Count);
            else
            {
                for (int g = 5; g < groups.Count && g < 9; g++) hint.Append(groups[g].Name).Append(" x").Append(groups[g].Count).AppendLine();
                if (groups.Count > 9) hint.Append("+").Append(groups.Count - 9).AppendLine(" more groups");
            }
            hint.Append("\nConsecutive identical jobs grouped. Count includes current job in first group.\nRead only; execution order unchanged.");
            tile.Hint.SetContent(hint.ToString());
        }
        if (progress != null && progress.transform.parent is RectTransform track)
        {
            track.anchorMin = track.anchorMax = track.pivot = Vector2.zero;
            track.anchoredPosition = new Vector2(8, 8); track.sizeDelta = new Vector2(rect.rect.width - 16, 6);
        }
    }
    private Tile CreateTile()
    {
        var go = new GameObject("WorkGroup", typeof(RectTransform), typeof(Image), typeof(CommandHintTarget));
        go.transform.SetParent(transform, false);
        var tile = new Tile { Rect = (RectTransform)go.transform, Background = go.GetComponent<Image>(), Hint = go.GetComponent<CommandHintTarget>() };
        tile.Hint.Initialize(tooltip);
        var picture = new GameObject("Icon", typeof(RectTransform), typeof(Image)); picture.transform.SetParent(go.transform, false);
        tile.Icon = picture.GetComponent<Image>(); tile.Icon.preserveAspect = true; tile.Icon.raycastTarget = false;
        var r = (RectTransform)picture.transform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(4, -3); r.sizeDelta = new Vector2(32, 30);
        tile.Name = TileLabel(go.transform, "Name", new Vector2(3, -35), new Vector2(0, 16));
        var nr = tile.Name.rectTransform; nr.anchorMax = new Vector2(1, 1); nr.sizeDelta = new Vector2(-6, 16);
        tile.Name.fontSize = 11; tile.Name.overflowMode = TextOverflowModes.Ellipsis;
        tile.Count = TileLabel(go.transform, "Count", new Vector2(-3, -2), new Vector2(30, 22));
        tile.Count.rectTransform.anchorMin = tile.Count.rectTransform.anchorMax = tile.Count.rectTransform.pivot = Vector2.one;
        tile.Count.alignment = TextAlignmentOptions.TopRight; tile.Count.fontSize = 17; tile.Count.fontStyle = FontStyles.Bold;
        tile.Count.color = new Color(1, .84f, .5f);
        return tile;
    }
    private TextMeshProUGUI TileLabel(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>(); label.font = body.font; label.color = new Color(.96f, .92f, .84f);
        label.raycastTarget = false; label.richText = false;
        var r = label.rectTransform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = pos; r.sizeDelta = size;
        return label;
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool WaitingForPopulation(EntityManager em, Entity producer, ProductionQueueElement item, int player)
    {
        if (item.RemainingWork > 0 || item.Kind != ProductionKind.Train ||
            !em.HasBuffer<ProductionElement>(producer) ||
            PlayerContextHelper.GetContextData(em, player, out var context) != FunctionResult.Success) return false;
        foreach (var offer in em.GetBuffer<ProductionElement>(producer, true))
        {
            if (offer.JobID != item.JobID || offer.Kind != item.Kind || !em.Exists(offer.UnitPrefab) ||
                !em.HasComponent<UnitComponent>(offer.UnitPrefab)) continue;
            int cost = em.GetComponentData<UnitComponent>(offer.UnitPrefab).PopulationCost;
            return cost > 0 && (long)context.currentPopulation + cost > context.maxPopulation;
        }
        return false;
    }
    private void SetVisible(bool visible)
    {
        if (group == null) return;
        if (embeddedInDock && !visible)
        {
            // Keep the reserved region stable instead of moving the other controls.
            if (body != null) { body.text = "No active work"; body.rectTransform.anchoredPosition = new Vector2(8, -4); }
            if (icon != null) icon.enabled = false;
            if (progress != null) progress.enabled = false;
            foreach (var tile in tiles) tile.Rect.gameObject.SetActive(false);
            visible = true;
        }
        group.alpha = visible ? 1 : 0;
        group.blocksRaycasts = visible;
        group.interactable = false;
    }
    private void OnDisable() => SetVisible(false);
}
