using System.Globalization;
using System.Text;
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
    [SerializeField] private string titleFormat = "Queue {0}/{1}";
    [SerializeField] private string pausedText = "Production paused";
    [SerializeField] private string populationText = "Ready - waiting for population";
    private float nextRefresh;
    private Entity previous;
    private World previousWorld;

    private void OnEnable() { nextRefresh = 0f; SetVisible(false); }
    private void LateUpdate()
    {
        // Menus can have multiple rows; queue always sits above the occupied cards.
        if (selectionPanelRoot != null && transform is RectTransform rect)
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
        if (!em.Exists(source) || !em.HasBuffer<ProductionQueueElement>(source) || !em.HasComponent<EntityWork>(source))
        { SetVisible(false); return; }
        if (em.HasComponent<EntityHealth>(source) && em.GetComponentData<EntityHealth>(source).CurrentHP <= 0)
        { SetVisible(false); return; }
        var queue = em.GetBuffer<ProductionQueueElement>(source, true);
        if (queue.Length == 0) { SetVisible(false); return; }
        SetVisible(true);
        var work = em.GetComponentData<EntityWork>(source);
        var first = queue[0];
        var text = new StringBuilder(string.Format(CultureInfo.CurrentCulture, titleFormat, queue.Length, work.JobCapacity));
        text.Append(" - ").Append(EntityPresentation.JobName(first.Offer, presentationRegistry));
        float totalWork = 0f;
        if (ProductionJobs.TryRegistry(em, out var registryEntity))
        {
            var registry = em.GetBuffer<RegistryBlobElement>(registryEntity, true);
            if (first.Offer.Kind == ProductionKind.Train)
            {
                var job = registry.GetBlobByID<TrainBlob>(first.Offer.JobID, out var found);
                if (found == FunctionResult.Success) totalWork = job.Value.Job.WorkLoad;
            }
            else if (first.Offer.Kind == ProductionKind.Research)
            {
                var job = registry.GetBlobByID<ResearchBlob>(first.Offer.JobID, out var found);
                if (found == FunctionResult.Success) totalWork = job.Value.Job.WorkLoad;
            }
        }
        bool progressKnown = Finite(totalWork) && totalWork > 0 && Finite(first.RemainingWork);
        float fraction = progressKnown
            ? Mathf.Clamp01(1f - first.RemainingWork / totalWork) : 0f;
        if (progress != null) progress.fillAmount = fraction;
        text.AppendLine();
        bool construction = em.HasComponent<BuildingConstruction>(source) &&
            em.GetComponentData<BuildingConstruction>(source).Phase != ConstructionPhase.Completed;
        if (construction) text.Append("Under construction");
        else if (WaitingForPopulation(em, first, player)) text.Append(populationText);
        else if (!progressKnown) text.Append("Progress unavailable");
        else if (!Finite(work.Rate) || work.Rate <= 0) text.Append(pausedText);
        else if (Finite(first.RemainingWork / work.Rate))
            text.AppendFormat(CultureInfo.CurrentCulture, "{0:0}% - ~{1:0.#}s remaining", fraction * 100f, Mathf.Max(0, first.RemainingWork / work.Rate));
        else text.Append("Time unavailable");
        if (queue.Length > 1)
        {
            text.AppendLine().Append("Next: ");
            for (int i = 1; i < queue.Length && i <= 2; i++)
            {
                if (i > 1) text.Append(" / ");
                text.Append(EntityPresentation.JobName(queue[i].Offer, presentationRegistry));
            }
            if (queue.Length > 3) text.Append(" +").Append(queue.Length - 3);
        }
        if (body != null && body.text != text.ToString()) body.text = text.ToString();
        if (icon != null)
        {
            icon.sprite = EntityPresentation.JobIcon(first.Offer, presentationRegistry);
            icon.enabled = icon.sprite != null;
        }
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool WaitingForPopulation(EntityManager em, ProductionQueueElement item, int player)
    {
        if (item.RemainingWork > 0 || item.Offer.Kind != ProductionKind.Train ||
            !em.Exists(item.Offer.UnitPrefab) || !em.HasComponent<UnitComponent>(item.Offer.UnitPrefab) ||
            PlayerContextHelper.GetContextData(em, player, out var context) != FunctionResult.Success) return false;
        int cost = em.GetComponentData<UnitComponent>(item.Offer.UnitPrefab).PopulationCost;
        return cost > 0 && (long)context.currentPopulation + cost > context.maxPopulation;
    }
    private void SetVisible(bool visible)
    {
        if (group == null) return;
        group.alpha = visible ? 1 : 0;
        group.blocksRaycasts = visible;
        group.interactable = false;
    }
    private void OnDisable() => SetVisible(false);
}
