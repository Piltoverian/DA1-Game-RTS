using UnityEngine;
using UnityEngine.UI;

public class InfoPanel : MonoBehaviour
{
    [SerializeField] private TMPro.TextMeshProUGUI nameText;

    public void SetInfoValue(float value)
    {
        SetInfoText(Mathf.Round(value).ToString());
    }

    public void SetInfoText(string value)
    {
        if (nameText != null && nameText.text != value) nameText.text = value;
    }

    [SerializeField] private RawImage icon;
    [SerializeField] private Image spriteIcon;
    public void SetInfoIcon(Sprite sprite)
    {
        if (spriteIcon != null)
        {
            spriteIcon.sprite = sprite;
            spriteIcon.enabled = sprite != null;
            spriteIcon.preserveAspect = true;
            spriteIcon.gameObject.SetActive(sprite != null);
            return;
        }
        if (icon == null) return;
        icon.enabled = sprite != null;
        if (sprite == null) return;
        icon.texture = sprite.texture;
    }
}
