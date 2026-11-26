using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>Local UI/settings only. Opening this menu does not pause simulation.</summary>
[DefaultExecutionOrder(-900)]
public class HudSystemMenu : MonoBehaviour
{
    [SerializeField] private Button menuButton;
    [SerializeField] private GameObject buttonPrefab;
    [SerializeField] private TextMeshProUGUI textTemplate;
    [SerializeField] private Sprite panelSkin;
    private static HudSystemMenu instance;
    private static int escapeFrame = -1;
    private GameObject overlay;
    private CanvasScaler scaler;
    private Vector2 baseResolution;
    private TextMeshProUGUI settingsLabel;
    private float uiScale = 1f, savedVolume = 1f;
    private GameObject previousFocus;
    private readonly List<Button> buttons = new();
    public static bool IsOpen => instance != null && instance.overlay != null && instance.overlay.activeSelf;
    public static bool HandledEscapeThisFrame => escapeFrame == Time.frameCount;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() { instance = null; escapeFrame = -1; }

    private void Awake()
    {
        if (instance != null && instance != this) { enabled = false; return; }
        instance = this;
        scaler = GetComponent<CanvasScaler>();
        if (scaler != null) baseResolution = scaler.referenceResolution;
        uiScale = Mathf.Clamp(PlayerPrefs.GetFloat("RTS.UI.Scale", 1f), .8f, 1.2f);
        ApplyScale();
        if (buttonPrefab == null || textTemplate == null) return;
        overlay = Rect("SystemMenuOverlay", transform, Vector2.zero, Vector2.zero).gameObject;
        var cover = overlay.AddComponent<Image>(); cover.color = new Color(0, .02f, .015f, .75f);
        var r = (RectTransform)overlay.transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.sizeDelta = Vector2.zero;
        var card = Rect("SystemMenuCard", overlay.transform, new Vector2(520, 530), Vector2.zero);
        var background = card.gameObject.AddComponent<Image>(); background.sprite = panelSkin;
        background.type = Image.Type.Sliced; background.color = Color.white;
        Label(card, "SECT COMMAND", new Vector2(0, 220), new Vector2(460, 36), 24, new Color(.94f, .83f, .58f));
        Label(card, "Simulation continues while this menu is open.", new Vector2(0, 178), new Vector2(460, 32), 16, new Color(.71f, .8f, .75f));
        AddButton(card, "Resume", 131, Close);
        settingsLabel = Label(card, "", new Vector2(0, 82), new Vector2(450, 24), 17, new Color(.96f, .92f, .84f));
        AddButton(card, "UI size -", 35, () => { uiScale = Mathf.Max(.8f, uiScale - .1f); ApplyScale(); });
        AddButton(card, "UI size +", -14, () => { uiScale = Mathf.Min(1.2f, uiScale + .1f); ApplyScale(); });
        AddButton(card, "Toggle sound", -63, () =>
        {
            if (AudioListener.volume > 0) { savedVolume = AudioListener.volume; AudioListener.volume = 0; }
            else AudioListener.volume = Mathf.Max(.01f, savedVolume);
            UpdateLabel();
        });
        AddButton(card, "Restart match", -112, () => { Close(); PlayableSessionController.Instance?.RestartMatch(); });
        AddButton(card, "Exit game", -161, () => Application.Quit());
        Label(card, "Left click: select | Right click: orders | Wheel: zoom\nWASD / arrows: camera | Shift: fast camera | Esc: menu", new Vector2(0, -223), new Vector2(470, 52), 15, new Color(.71f, .8f, .75f));
        for (int i = 0; i < buttons.Count; i++)
        {
            var navigation = buttons[i].navigation; navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = buttons[(i + buttons.Count - 1) % buttons.Count];
            navigation.selectOnDown = buttons[(i + 1) % buttons.Count]; buttons[i].navigation = navigation;
        }
        overlay.SetActive(false);
        if (menuButton != null) menuButton.onClick.AddListener(Open);
        UpdateLabel();
    }
    private void Update()
    {
        if (!Application.isFocused || PlayableSessionController.BlocksInput || GameplayInputGate.IsEditingText || Keyboard.current == null ||
            !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        if (IsOpen) { escapeFrame = Time.frameCount; Close(); }
        else if (!BuildingPlacer.IsPlacementActive && !ResourceInspection.TryGet(out _))
        { escapeFrame = Time.frameCount; Open(); }
    }
    private void ApplyScale()
    {
        if (scaler != null) scaler.referenceResolution = baseResolution / uiScale;
        PlayerPrefs.SetFloat("RTS.UI.Scale", uiScale);
        UpdateLabel();
    }
    private void UpdateLabel()
    {
        if (settingsLabel != null) settingsLabel.text = $"UI size: {uiScale * 100f:0}%  |  Sound: {(AudioListener.volume > 0 ? "On" : "Off")}";
    }
    public void Open()
    {
        if (overlay == null || IsOpen) return;
        previousFocus = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        overlay.transform.SetAsLastSibling(); overlay.SetActive(true); UpdateLabel();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(buttons.Count > 0 ? buttons[0].gameObject : null);
    }
    public void Close()
    {
        if (overlay == null) return;
        overlay.SetActive(false); GameplayInputGate.SuppressWorldInputUntilRelease();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousFocus != null && previousFocus.activeInHierarchy ? previousFocus : null);
    }
    private void OnDisable() { if (overlay != null) Close(); }
    private void OnDestroy()
    {
        if (menuButton != null) menuButton.onClick.RemoveListener(Open);
        if (instance == this) instance = null;
        if (overlay != null) Destroy(overlay);
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
    }
    private TextMeshProUGUI Label(Transform parent, string text, Vector2 position, Vector2 size, int fontSize, Color tint)
    {
        var label = Instantiate(textTemplate, parent); label.name = "MenuLabel";
        label.gameObject.SetActive(true); label.enabled = true; label.text = text;
        label.fontSize = fontSize; label.color = tint; label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = fontSize >= 24 ? FontStyles.Bold : FontStyles.Normal;
        label.raycastTarget = false; label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, .5f);
        label.rectTransform.pivot = new Vector2(.5f, .5f); label.rectTransform.anchoredPosition = position;
        label.rectTransform.sizeDelta = size; return label;
    }
    private void AddButton(Transform parent, string title, float y, UnityEngine.Events.UnityAction action)
    {
        var go = Instantiate(buttonPrefab, parent); go.name = title;
        var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(0, y); rect.sizeDelta = new Vector2(300, 40);
        foreach (var image in go.GetComponentsInChildren<Image>(true)) if (image.gameObject.name == "Icon") image.gameObject.SetActive(false);
        var badge = go.transform.Find("StatusBadge"); if (badge != null) badge.gameObject.SetActive(false);
        var label = go.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
        {
            label.text = title; label.fontSize = 18; label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.pivot = new Vector2(.5f, .5f); label.rectTransform.anchoredPosition = Vector2.zero;
            label.rectTransform.sizeDelta = new Vector2(-24, -8);
        }
        var button = go.GetComponent<Button>(); button.onClick.RemoveAllListeners(); button.onClick.AddListener(action);
        buttons.Add(button);
    }
}
