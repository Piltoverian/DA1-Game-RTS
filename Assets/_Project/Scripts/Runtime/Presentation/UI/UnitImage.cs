using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UnitImage : MonoBehaviour
{
    [SerializeField] private Image image;
    [SerializeField] private TextMeshProUGUI missingIconText;

    // View only: UnitInfoPanel supplies the same representative as the text/HP views.
    public void SetPortrait(Sprite sprite, bool hasSelection)
    {
        if (image == null) image = GetComponent<Image>();
        if (image != null)
        {
            var displayedSprite = hasSelection ? sprite : null;
            if (image.sprite != displayedSprite) image.sprite = displayedSprite;
            image.enabled = displayedSprite != null;
        }

        if (missingIconText != null)
            missingIconText.enabled = hasSelection && sprite == null;
    }
}

