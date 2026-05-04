using UnityEngine;

public class ResourceInfo : MonoBehaviour
{
    public TMPro.TextMeshProUGUI nameText;

    public void SetInfo(float value)
    {
        nameText.text = Mathf.Round(value).ToString();
    }
}
