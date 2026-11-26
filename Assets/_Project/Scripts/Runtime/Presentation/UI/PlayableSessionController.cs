using System.Collections;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>The Main demo's match lifecycle. Simulation orders remain in the existing command pipeline.</summary>
public sealed class PlayableSessionController : MonoBehaviour
{
    public static PlayableSessionController Instance { get; private set; }
    public static bool BlocksInput => Instance != null && (Instance.Ended || Instance.restarting);
    public bool Ended { get; private set; }
    public bool Won { get; private set; }
    [SerializeField] private GameObject buttonPrefab;
    [SerializeField] private TextMeshProUGUI textTemplate;
    [SerializeField] private Sprite panelSkin;
    [SerializeField] private bool enableOpponent = true;
    private TextMeshProUGUI objective;
    private GameObject result;
    private World world;
    private bool hadBothBases, framed, restarting;
    private float nextTick, elapsed;
    private readonly SkirmishOpponent opponent = new SkirmishOpponent();

    private void Awake()
    {
        Instance = this; Application.targetFrameRate = 60; Application.runInBackground = true;
        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-rts-verify") >= 0) gameObject.AddComponent<PlayablePlayerProbe>();
        var objectivePanel = new GameObject("ObjectivePanel", typeof(RectTransform), typeof(Image)); objectivePanel.transform.SetParent(transform, false);
        var panelRect = (RectTransform)objectivePanel.transform;
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0,1);
        panelRect.anchoredPosition = new Vector2(12,-78); panelRect.sizeDelta = new Vector2(740,58);
        var panelImage = objectivePanel.GetComponent<Image>(); panelImage.sprite = panelSkin; panelImage.type = Image.Type.Sliced; panelImage.raycastTarget = false;
        objective = Label(objectivePanel.transform, "Preparing battlefield...", new Vector2(712, 44), 16);
        var rect = objective.rectTransform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(14, -7);
        objective.alignment = TextAlignmentOptions.TopLeft;
    }
    private void Update()
    {
        if (restarting || Ended) return;
        var current = World.DefaultGameObjectInjectionWorld;
        if (current == null || !current.IsCreated) return;
        if (current.IsClient() || current.IsServer())
        { objective.text = "Multiplayer match"; return; }
        if (!SelectHelper.TryGetLocalPlayerId(out int local)) return;
        if (world != current) { world = current; elapsed = 0; framed = hadBothBases = false; opponent.Reset(); }
        var em = current.EntityManager;
        using (var bootstrap = em.CreateEntityQuery(typeof(PlayerBootstrapState)))
        {
            if (bootstrap.CalculateEntityCount() != 1) return;
            var phase = bootstrap.GetSingleton<PlayerBootstrapState>().Phase;
            if (phase == PlayerBootstrapPhase.Failed) { objective.text = "Battlefield could not start. See Console / log."; return; }
            if (phase != PlayerBootstrapPhase.Ready) return;
        }
        elapsed += Time.deltaTime;
        if (Time.unscaledTime < nextTick) return;
        nextTick = Time.unscaledTime + .5f;
        Entity home = Entity.Null, enemy = Entity.Null;
        int enemyPlayer = -1;
        using (var bases = em.CreateEntityQuery(typeof(MainBaseTag), typeof(EntityOwner), typeof(EntityHealth), typeof(LocalTransform)))
        using (var entities = bases.ToEntityArray(Allocator.Temp))
        {
            foreach (var entity in entities)
            {
                if (em.GetComponentData<EntityHealth>(entity).CurrentHP <= 0) continue;
                int owner = em.GetComponentData<EntityOwner>(entity).PlayerID;
                if (owner == local) home = entity;
                else if (enemy == Entity.Null) { enemy = entity; enemyPlayer = owner; }
            }
        }
        if (home != Entity.Null && enemy != Entity.Null) hadBothBases = true;
        if (hadBothBases && (home == Entity.Null || enemy == Entity.Null)) { Finish(home != Entity.Null); return; }
        if (home == Entity.Null || enemy == Entity.Null) return;
        if (!framed && Camera.main != null)
        {
            var camera = Camera.main; var position = (Vector3)em.GetComponentData<LocalTransform>(home).Position;
            camera.transform.position = position - camera.transform.forward * 60;
            var setup = camera.GetComponent<IsometricTerrainCamera>(); if (setup != null) { setup.size = 23; setup.Apply(); }
            framed = true;
        }
        objective.text = "OBJECTIVE: Destroy the enemy TownHall  |  Enemy HP: " +
            Mathf.CeilToInt(em.GetComponentData<EntityHealth>(enemy).CurrentHP) +
            "\nGather resources → build Barracks / defenses → train soldiers. Right-click to give orders.";
        if (enableOpponent) opponent.Tick(em, enemyPlayer, enemy, home, elapsed);
    }
    private void Finish(bool victory)
    {
        Ended = true; Won = victory; ResourceInspection.Clear();
        if (HudSystemMenu.IsOpen) FindFirstObjectByType<HudSystemMenu>()?.Close();
        objective.text = victory ? "VICTORY — enemy TownHall destroyed" : "DEFEAT — your TownHall was destroyed";
        result = new GameObject("MatchResult", typeof(RectTransform), typeof(Image)); result.transform.SetParent(transform, false);
        var rect = (RectTransform)result.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        result.GetComponent<Image>().color = new Color(.01f, .03f, .025f, .85f);
        var card = new GameObject("ResultCard", typeof(RectTransform), typeof(Image)); card.transform.SetParent(result.transform, false);
        var cr = (RectTransform)card.transform; cr.sizeDelta = new Vector2(560, 310);
        var bg = card.GetComponent<Image>(); bg.sprite = panelSkin; bg.type = Image.Type.Sliced;
        var title = Label(card.transform, victory ? "VICTORY" : "DEFEAT", new Vector2(500, 55), 32); title.rectTransform.anchoredPosition = new Vector2(0, 90);
        var detail = Label(card.transform, victory ? "The enemy stronghold has fallen." : "Your stronghold has fallen. Gather, fortify and try again.", new Vector2(500, 52), 18);
        detail.rectTransform.anchoredPosition = new Vector2(0, 30);
        AddButton(card.transform, "Play again", -45, RestartMatch);
        AddButton(card.transform, "Exit game", -100, () => Application.Quit());
    }
    public void RestartMatch()
    {
        var current = World.DefaultGameObjectInjectionWorld;
        if (current != null && current.IsCreated && (current.IsClient() || current.IsServer())) return;
        if (!restarting) StartCoroutine(Restart());
    }
    private IEnumerator Restart()
    {
        restarting = true; Time.timeScale = 1; ResourceInspection.Clear();
        yield return null;
        int scene = SceneManager.GetActiveScene().buildIndex;
        var oldWorld = World.DefaultGameObjectInjectionWorld;
        if (oldWorld != null && oldWorld.IsCreated)
        {
            ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(oldWorld);
            oldWorld.Dispose();
        }
        World.DefaultGameObjectInjectionWorld = null;
        DefaultWorldInitialization.Initialize("Default World", false);
        SceneManager.LoadScene(scene);
    }
    private TextMeshProUGUI Label(Transform parent, string text, Vector2 size, int fontSize)
    {
        var go = new GameObject("SessionText", typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>(); label.font = textTemplate.font;
        label.text = text; label.fontSize = fontSize; label.color = new Color(.96f, .88f, .68f); label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.Center; label.fontStyle = FontStyles.Normal;
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(.5f, .5f);
        label.rectTransform.sizeDelta = size; label.rectTransform.anchoredPosition = Vector2.zero; return label;
    }
    private void AddButton(Transform parent, string text, float y, UnityEngine.Events.UnityAction action)
    {
        var go = Instantiate(buttonPrefab, parent); var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(300, 42); rect.anchoredPosition = new Vector2(0, y);
        foreach (var image in go.GetComponentsInChildren<Image>(true)) if (image.gameObject.name == "Icon") image.gameObject.SetActive(false);
        foreach (string badge in new[] { "StatusBadge", "QueueCount" }) { var child = go.transform.Find(badge); if (child != null) child.gameObject.SetActive(false); }
        var label = go.GetComponentInChildren<TextMeshProUGUI>(); label.text = text; label.fontSize = 18; label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.anchoredPosition = Vector2.zero; label.rectTransform.sizeDelta = new Vector2(-20, -6);
        var button = go.GetComponent<Button>(); button.onClick.RemoveAllListeners(); button.onClick.AddListener(action);
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }
}
