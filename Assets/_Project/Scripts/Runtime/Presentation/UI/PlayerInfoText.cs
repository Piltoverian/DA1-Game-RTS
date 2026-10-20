using System.Globalization;
using TMPro;
using UnityEngine;

/// <summary>Displays only identity and age published by the local PlayerContext.</summary>
public class PlayerInfoText : UIComponent
{
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private string playerFormat = "Player {0}";
    [SerializeField] private string unavailableText = "Player --\nAge --";
    [SerializeField] private string industrialText = "Industrial";
    [SerializeField] private string modernText = "Modern";
    [SerializeField] private string futureText = "Future";
    [SerializeField] private string unknownAgeText = "Age --";
    private int displayedId = -1;
    private Age displayedAge;
    private bool hasContext;

    protected override void OnPlayerContextUpdated(PlayerContext context)
    {
        if (hasContext && displayedId == context.PlayerId && displayedAge == context.age) return;
        hasContext = true;
        displayedId = context.PlayerId;
        displayedAge = context.age;
        string age = context.age switch
        {
            Age.Industrial => industrialText,
            Age.Modern => modernText,
            Age.Future => futureText,
            _ => unknownAgeText
        };
        if (label != null)
            label.text = string.Format(CultureInfo.CurrentCulture, playerFormat, context.PlayerId) + "\n" + age;
    }

    protected override void OnContextUnavailable()
    {
        hasContext = false;
        displayedId = -1;
        if (label != null) label.text = unavailableText;
    }
}
