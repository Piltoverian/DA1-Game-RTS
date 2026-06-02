using UnityEngine;
using UnityEngine.UI;

public class InfoPanel : MonoBehaviour
{
    [SerializeField] private TMPro.TextMeshProUGUI nameText;

    public void SetInfoValue(float value)
    {
        nameText.text = Mathf.Round(value).ToString();
    }

    [SerializeField] private RawImage icon;
    public void SetInfoIcon(Sprite sprite)
    {
        icon.texture = sprite.texture;
    }
}
