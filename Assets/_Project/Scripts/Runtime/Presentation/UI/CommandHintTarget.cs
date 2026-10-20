using UnityEngine;
using UnityEngine.EventSystems;

public class CommandHintTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    private CommandTooltipView view;
    private string content;
    private bool hovered;
    private bool selected;

    public void Initialize(CommandTooltipView tooltip) => view = tooltip;

    public void SetContent(string value)
    {
        if (content == value) return;
        content = value;
        Refresh();
    }

    private void Refresh()
    {
        if (view == null) return;
        if (hovered || selected) view.Show(this, content);
        else view.Hide(this);
    }

    public void OnPointerEnter(PointerEventData data) { hovered = true; Refresh(); }
    public void OnPointerExit(PointerEventData data) { hovered = false; Refresh(); }
    public void OnSelect(BaseEventData data) { selected = true; Refresh(); }
    public void OnDeselect(BaseEventData data) { selected = false; Refresh(); }
    public void Clear()
    {
        hovered = selected = false;
        content = null;
        if (view != null) view.Hide(this);
    }
    private void OnDisable() => Clear();
}
