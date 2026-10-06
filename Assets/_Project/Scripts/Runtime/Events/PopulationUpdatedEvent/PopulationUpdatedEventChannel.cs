using UnityEngine;
[CreateAssetMenu(fileName = "PopulationUpdatedEventChannel", menuName = "Events/PopulationUpdatedEventChannel")]
public class PopulationUpdatedEventChannel : EventChannel<PopulationUpdatedEvent>
{
}

public struct PopulationUpdatedEvent : IEvent
{
    public int PlayerId;
    public int CurrentPopulation;
    public int MaxPopulation;
    public PopulationUpdatedEvent(int playerId, int currentPopulation, int maxPopulation)
    {
        PlayerId = playerId;
        CurrentPopulation = currentPopulation;
        MaxPopulation = maxPopulation;
    }
}
