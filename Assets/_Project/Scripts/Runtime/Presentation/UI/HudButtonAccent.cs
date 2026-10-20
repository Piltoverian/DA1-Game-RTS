using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UISelectable = UnityEngine.UI.Selectable;

/// <summary>Brass/jade focus feedback only; does not add button listeners or commands.</summary>
public class HudButtonAccent : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private Graphic frame;
    [SerializeField] private Color normal = new Color(.63f, .49f, .28f, .8f);
    [SerializeField] private Color hover = new Color(.63f, .95f, .81f, 1f);
    [SerializeField] private Color pressed = new Color(1f, .87f, .56f, 1f);
    [SerializeField] private Color disabled = new Color(.38f, .43f, .4f, 1f);
    private UISelectable selectable;
    private bool hovered, down, focused, wasInteractable;

    private void Awake() => selectable = GetComponent<UISelectable>();
    private void OnEnable() { hovered = down = focused = false; Apply(); }
    private void LateUpdate()
    {
        bool interactable = selectable != null && selectable.IsInteractable();
        if (interactable != wasInteractable) Apply();
    }
    private void Apply()
    {
        wasInteractable = selectable != null && selectable.IsInteractable();
        if (frame == null) return;
        Color tint = !wasInteractable ? disabled : down ? pressed : hovered || focused ? hover : normal;
        if (frame.color != tint) frame.color = tint;
    }
    public void OnPointerEnter(PointerEventData data) { hovered = true; Apply(); }
    public void OnPointerExit(PointerEventData data) { hovered = down = false; Apply(); }
    public void OnPointerDown(PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Left) return;
        down = true; Apply();
    }
    public void OnPointerUp(PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Left) return;
        down = false; Apply();
    }
    public void OnSelect(BaseEventData data) { focused = true; Apply(); }
    public void OnDeselect(BaseEventData data) { focused = false; Apply(); }
    private void OnDisable() { hovered = down = focused = false; }
}
