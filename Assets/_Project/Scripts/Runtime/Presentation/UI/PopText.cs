using System.Globalization;
using TMPro;
using Unity.Entities;
using UnityEngine;

public class PopText : UIComponent
{
    [SerializeField] TextMeshProUGUI m_TextMeshPro;
    [SerializeField] private string populationFormat = "Pop: {0}/{1}";
    [SerializeField] private string unavailableText = "Pop: --/--";
    [SerializeField] private Color normalColor = new Color(0.94f, 0.91f, 0.8f, 1f);
    [SerializeField] private Color fullColor = new Color(1f, 0.76f, 0.38f, 1f);

    protected override void OnContextUnavailable()
    {
        if (m_TextMeshPro != null)
        {
            m_TextMeshPro.text = unavailableText;
            m_TextMeshPro.color = normalColor;
        }
    }

    protected override void OnPlayerSnapshot(EntityManager manager, Entity entity, PlayerContext context) =>
        SetPopulation(context.currentPopulation, context.maxPopulation);

    private void SetPopulation(int current, int maximum)
    {
        if (m_TextMeshPro == null) return;
        var text = string.Format(CultureInfo.CurrentCulture, populationFormat, current, maximum);
        if (m_TextMeshPro.text != text) m_TextMeshPro.text = text;
        m_TextMeshPro.color = current >= maximum ? fullColor : normalColor;
    }

    public void OnPopChange(PopulationUpdatedEvent populationUpdatedEvent)
    {
        if (playerId < 0 || populationUpdatedEvent.PlayerId != playerId)
        {
            return;
        }

        SetPopulation(populationUpdatedEvent.CurrentPopulation, populationUpdatedEvent.MaxPopulation);
    }
}
