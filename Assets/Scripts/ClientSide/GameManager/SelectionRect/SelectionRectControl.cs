using gameManagerModule;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class SelectionRectViewModule : MonoBehaviour
{
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private StartEndRect selectingRect;
    [SerializeField] private RectTransform canvasRect;
    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    void Update()
    {
        Debug.Log(selectingRect);
        selectingRect =
            GameManager.Instance.GetModule<SelectManager>()
            .GetCurrentSelectionRect();

        if (selectingRect.isNotNull)
        {
            SetVisible(true);
            UpdateRect(selectingRect.StartPoint, selectingRect.EndPoint);
            gameObject.SetActive(true);
        }
        else
        {
            SetVisible(false);
            
        }
    }

    void SetVisible(bool visible)
    {
        foreach (var obj in GetComponentsInChildren<Image>())
        {
            obj.enabled = visible;
        }
    }

    void UpdateRect(Vector2 start, Vector2 end)
    {
        start=ScreenToCanvas(start);
        end=ScreenToCanvas(end);
        float minX = Mathf.Min(start.x, end.x);
        float minY = Mathf.Min(start.y, end.y);
        float width = Mathf.Abs(start.x - end.x);
        float height = Mathf.Abs(start.y - end.y);

        rectTransform.anchoredPosition = new Vector2(minX, minY);
        rectTransform.sizeDelta = new Vector2(width, height);
    }

    Vector2 ScreenToCanvas(Vector2 screenPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPos,
            null, // Screen Space - Overlay
            out Vector2 localPos
        );

        Vector2 pivotOffset = new Vector2(
        canvasRect.rect.width * canvasRect.pivot.x,
        canvasRect.rect.height * canvasRect.pivot.y
    );
        return localPos+pivotOffset;
    }

}