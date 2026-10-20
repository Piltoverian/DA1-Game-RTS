using TMPro;
using UnityEngine;

public class CommandTooltipView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI body;
    [SerializeField] private CanvasGroup group;
    private CommandHintTarget owner;
    private RectTransform rect;
    private RectTransform canvas;

    private void Awake()
    {
        rect = transform as RectTransform;
        var parentCanvas = GetComponentInParent<Canvas>();
        canvas = parentCanvas != null ? parentCanvas.transform as RectTransform : null;
        Hide(null);
    }

    public void Show(CommandHintTarget target, string content)
    {
        owner = target;
        if (body == null || group == null || rect == null || canvas == null) { Hide(null); return; }
        if (body.text != content)
        {
            body.text = content;
            float height = body.GetPreferredValues(content, rect.rect.width - 32f, Mathf.Infinity).y + 32f;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, Mathf.Clamp(height, 80f, 260f));
        }
        group.alpha = 1f;
        transform.SetAsLastSibling();
        Position();
    }

    public void Hide(CommandHintTarget target)
    {
        if (target != null && target != owner) return;
        owner = null;
        if (group != null) group.alpha = 0f;
    }

    private void LateUpdate()
    {
        if (owner == null || !owner.isActiveAndEnabled) { Hide(null); return; }
        Position();
    }

    private void Position()
    {
        if (owner == null || canvas == null || rect == null) return;
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas, owner.transform);
        float x = bounds.min.x - canvas.rect.xMin;
        float y = bounds.max.y - canvas.rect.yMin + 8f;
        if (y + rect.rect.height > canvas.rect.height - 8f)
            y = bounds.min.y - canvas.rect.yMin - rect.rect.height - 8f;
        x = Mathf.Clamp(x, 8f, Mathf.Max(8f, canvas.rect.width - rect.rect.width - 8f));
        y = Mathf.Clamp(y, 8f, Mathf.Max(8f, canvas.rect.height - rect.rect.height - 8f));
        rect.anchoredPosition = new Vector2(x, y);
    }

    private void OnDisable() => Hide(null);
}
