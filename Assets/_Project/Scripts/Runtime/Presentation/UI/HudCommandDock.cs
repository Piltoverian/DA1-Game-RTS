using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Presentation layout only. Keeps the four control regions in one bottom frame.</summary>
[DefaultExecutionOrder(-800)]
public sealed class HudCommandDock : MonoBehaviour
{
    [SerializeField] private RectTransform info, actions, activity, minimap;
    [SerializeField] private TextMeshProUGUI textTemplate;
    private readonly RectTransform[] headings = new RectTransform[4];
    private readonly RectTransform[] dividers = new RectTransform[3];
    private RectTransform dock, canvas;
    private TextMeshProUGUI emptyActions;
    private void Awake()
    {
        dock = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>().transform as RectTransform;
        string[] titles = { "SELECTED OBJECT", "ACTIONS", "WORK / QUEUE", "TACTICAL MAP" };
        for (int i = 0; i < titles.Length; i++)
        {
            var go = new GameObject(titles[i], typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = textTemplate.font; label.fontSize = 14; label.fontStyle = FontStyles.Bold;
            label.color = new Color(.94f, .83f, .58f); label.text = titles[i];
            label.raycastTarget = false;
            headings[i] = (RectTransform)go.transform;
            if (i > 0)
            {
                var line = new GameObject("RegionDivider", typeof(RectTransform), typeof(Image));
                line.transform.SetParent(transform, false);
                var graphic = line.GetComponent<Image>(); graphic.color = new Color(.65f, .53f, .31f, .55f); graphic.raycastTarget = false;
                dividers[i - 1] = (RectTransform)line.transform;
            }
        }
        var empty = new GameObject("NoActions", typeof(RectTransform), typeof(TextMeshProUGUI));
        empty.transform.SetParent(transform, false); emptyActions = empty.GetComponent<TextMeshProUGUI>();
        emptyActions.font = textTemplate.font; emptyActions.fontSize = 14;
        emptyActions.text = "No actions available"; emptyActions.color = new Color(.71f, .8f, .75f); emptyActions.raycastTarget = false;
        Layout();
    }
    private void LateUpdate() => Layout();
    private void Layout()
    {
        if (canvas == null || info == null || actions == null || activity == null || minimap == null) return;
        float available = Mathf.Max(1, canvas.rect.width - 24);
        float scale = Mathf.Min(1, available / 1140f);
        float width = available / scale;
        float height = Mathf.Max(248, actions.rect.height + 64);
        dock.anchorMin = dock.anchorMax = dock.pivot = Vector2.zero;
        dock.anchoredPosition = new Vector2(12, 12);
        dock.localScale = new Vector3(scale, scale, 1);
        dock.sizeDelta = new Vector2(width, height);
        float queueWidth = width - 280 - 312 - 220 - 64;
        float[] x = { 16, 308, 632, width - 236 };
        float[] w = { 280, 312, queueWidth, 220 };
        for (int i = 0; i < 4; i++) Place(headings[i], x[i], 12, w[i], 22);
        for (int i = 0; i < 3; i++) Place(dividers[i], x[i + 1] - 6, 40, 1, height - 56);
        Place(info, x[0], 48, 280, 144);
        // CommandMenu owns grid size; the dock owns its position and outer frame.
        Place(actions, x[1], 44, actions.sizeDelta.x, actions.sizeDelta.y);
        bool hasActions = false;
        foreach (Transform child in actions) if (child.gameObject.activeSelf) { hasActions = true; break; }
        emptyActions.enabled = !hasActions;
        Place(emptyActions.rectTransform, x[1] + 8, 48, 296, 40);
        Place(activity, x[2], 40, queueWidth, height - 52);
        Place(minimap, x[3], 36, 220, height - 48);
        var image = GetComponent<Image>();
        if (image != null) image.enabled = true;
    }
    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
    }
}
