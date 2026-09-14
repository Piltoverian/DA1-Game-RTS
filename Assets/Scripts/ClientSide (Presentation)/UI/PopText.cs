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
            return;
        }

        m_TextMeshPro.text = "Pop: " + populationUpdatedEvent.CurrentPopulation+"/"+populationUpdatedEvent.MaxPopulation;
    }
}
