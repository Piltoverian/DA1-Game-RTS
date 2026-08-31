using System.ComponentModel;
using TMPro;
using UnityEngine;

public class PopText : UIComponent
{
    [SerializeField] TextMeshProUGUI m_TextMeshPro;

    public void OnPopChange(PopulationUpdatedEvent populationUpdatedEvent)
    {
        if (populationUpdatedEvent.PlayerId != playerId)
        {
            Debug.LogWarning($"PopText: Received PopulationUpdatedEvent for playerId {populationUpdatedEvent.PlayerId}, but this UI is for playerId {playerId}. Ignoring.");
            return;
        }

        Debug.Log("PopText: OnPopChange: " + populationUpdatedEvent.CurrentPopulation + "/" + populationUpdatedEvent.MaxPopulation);
        m_TextMeshPro.text = "Pop: " + populationUpdatedEvent.CurrentPopulation+"/"+populationUpdatedEvent.MaxPopulation;
    }
}
