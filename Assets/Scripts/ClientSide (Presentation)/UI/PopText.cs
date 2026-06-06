using TMPro;
using UnityEngine;

public class PopText : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI m_TextMeshPro;

    public void OnPopChange(PopulationUpdatedEvent populationUpdatedEvent)
    {
        m_TextMeshPro.text = "Pop: " + populationUpdatedEvent.CurrentPopulation+"/"+populationUpdatedEvent.MaxPopulation;
    }
}
