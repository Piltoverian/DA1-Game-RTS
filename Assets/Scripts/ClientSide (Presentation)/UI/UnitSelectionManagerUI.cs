//using UnityEngine;

//public class UnitSelectionManagerUI : MonoBehaviour
//{
//    [SerializeField] private RectTransform selectionAreaTransform;
//    private void Start()
//    {
//        UnitSelectionManager.Instance.OnSelectionAreaStart += UnitSelectionManager_OnSelectionAreaStart;
//        UnitSelectionManager.Instance.OnSelectionAreaEnd += UnitSelectionManager_OnSelectionAreaEnd;

//        selectionAreaTransform.gameObject.SetActive(false);
//    }
//    private void Update()
//    {
//        if (selectionAreaTransform.gameObject.activeSelf)
//        {
//                        UpdateVisual();
//        }
//    }
//    private void UnitSelectionManager_OnSelectionAreaStart(object sender, System.EventArgs e)
//    {
//        selectionAreaTransform.gameObject.SetActive(true);
//        UpdateVisual();
//    }
//    private void UnitSelectionManager_OnSelectionAreaEnd(object sender, System.EventArgs e)
//    {
//        selectionAreaTransform.gameObject.SetActive(false);
//    }
//    private void UpdateVisual()
//    {
//        Rect selectArea = UnitSelectionManager.Instance.GetSelectionArea();
//        selectionAreaTransform.anchoredPosition = selectArea.position;
//        selectionAreaTransform.sizeDelta = selectArea.size;
//    }
//}
