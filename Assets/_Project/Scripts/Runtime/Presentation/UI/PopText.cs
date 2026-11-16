using TMPro;
using UnityEngine;

public class PopText : UIComponent
{
    [SerializeField] TextMeshProUGUI m_TextMeshPro;

    private void LateUpdate()
    {
        // Events are not replayed when the canvas loads late or is re-enabled.
        if (!TryGetPlayer(out var em)
            || PlayerContextHelper.GetContextData(em, playerId, out var context) != FunctionResult.Success) return;
        ShowPopulation(context.currentPopulation, context.maxPopulation);
    }

    public void OnPopChange(PopulationUpdatedEvent populationUpdatedEvent)
    {
        if (!TryGetPlayer(out _) || populationUpdatedEvent.PlayerId != playerId)
        {
            return;
        }

        ShowPopulation(populationUpdatedEvent.CurrentPopulation, populationUpdatedEvent.MaxPopulation);
    }

    private void ShowPopulation(int current, int maximum)
    {
        if (m_TextMeshPro == null) return;
        var value = "Pop: " + current + "/" + maximum;
        if (m_TextMeshPro.text != value) m_TextMeshPro.text = value;
    }
}
