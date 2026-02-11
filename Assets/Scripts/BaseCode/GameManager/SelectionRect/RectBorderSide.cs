using UnityEngine;
using UnityEngine.UI;

public class RectBorderSide : MonoBehaviour
{
    public enum Side { Top, Bottom, Left, Right }
    [SerializeField]public Side side;
    public float thickness = 2f;

    RectTransform rt;
    RectTransform parent;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        parent = transform.parent.GetComponent<RectTransform>();
    }

    void LateUpdate()
    {
        float w = parent.rect.width;
        float h = parent.rect.height;

        switch (side)
        {
            case Side.Top:
                rt.anchoredPosition = new Vector2(0, h - thickness);
                rt.sizeDelta = new Vector2(w, thickness);
                break;
            case Side.Bottom:
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(w, thickness);
                break;
            case Side.Left:
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(thickness, h);
                break;
            case Side.Right:
                rt.anchoredPosition = new Vector2(w - thickness, 0);
                rt.sizeDelta = new Vector2(thickness, h);
                break;
        }
    }
}
