using UnityEngine;
using UnityEngine.UI;

/// <summary>Small brass inlay frame; geometry stays sharp at any canvas scale.</summary>
[AddComponentMenu("UI/HUD Frame")]
public class HudFrameGraphic : MaskableGraphic
{
    [SerializeField, Min(0.5f)] private float thickness = 1.5f;
    [SerializeField] private bool ornamental = true;

    protected override void OnEnable()
    {
        base.OnEnable();
        raycastTarget = false;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        if (r.width <= 0f || r.height <= 0f) return;
        float t = Mathf.Min(thickness, Mathf.Min(r.width, r.height) * 0.5f);
        Quad(vh, r.xMin, r.yMin, r.xMax, r.yMin + t);
        Quad(vh, r.xMin, r.yMax - t, r.xMax, r.yMax);
        Quad(vh, r.xMin, r.yMin + t, r.xMin + t, r.yMax - t);
        Quad(vh, r.xMax - t, r.yMin + t, r.xMax, r.yMax - t);
        if (!ornamental || Mathf.Min(r.width, r.height) < 24f) return;
        float inset = 3.5f;
        float fine = Mathf.Min(.65f, t);
        Quad(vh, r.xMin + inset, r.yMin + inset, r.xMax - inset, r.yMin + inset + fine);
        Quad(vh, r.xMin + inset, r.yMax - inset - fine, r.xMax - inset, r.yMax - inset);
        Quad(vh, r.xMin + inset, r.yMin + inset, r.xMin + inset + fine, r.yMax - inset);
        Quad(vh, r.xMax - inset - fine, r.yMin + inset, r.xMax - inset, r.yMax - inset);
        // Four tiny lozenge inlays suggest talisman corners without covering the portrait.
        Diamond(vh, new Vector2(r.xMin + inset, r.yMin + inset), 2f);
        Diamond(vh, new Vector2(r.xMin + inset, r.yMax - inset), 2f);
        Diamond(vh, new Vector2(r.xMax - inset, r.yMin + inset), 2f);
        Diamond(vh, new Vector2(r.xMax - inset, r.yMax - inset), 2f);
    }

    private void Diamond(VertexHelper vh, Vector2 center, float radius)
    {
        int start = vh.currentVertCount;
        vh.AddVert(center + Vector2.left * radius, color, Vector2.zero);
        vh.AddVert(center + Vector2.up * radius, color, Vector2.zero);
        vh.AddVert(center + Vector2.right * radius, color, Vector2.zero);
        vh.AddVert(center + Vector2.down * radius, color, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    private void Quad(VertexHelper vh, float left, float bottom, float right, float top)
    {
        int start = vh.currentVertCount;
        vh.AddVert(new Vector3(left, bottom), color, Vector2.zero);
        vh.AddVert(new Vector3(left, top), color, Vector2.zero);
        vh.AddVert(new Vector3(right, top), color, Vector2.zero);
        vh.AddVert(new Vector3(right, bottom), color, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }
}
