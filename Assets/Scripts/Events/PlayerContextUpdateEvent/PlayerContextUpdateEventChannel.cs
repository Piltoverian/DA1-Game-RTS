using UnityEngine;


[CreateAssetMenu(menuName = "Events/PlayerContextUpdateEventChannel")]
public class PlayerContextUpdateEventChannel : EventChannel<PlayerContextUpdateEvent>
{
}

public struct PlayerContextUpdateEvent:IEvent
{
    public PlayerContext playerContext;
}
