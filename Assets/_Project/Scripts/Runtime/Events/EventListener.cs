using UnityEngine;
using UnityEngine.Events;

public abstract class EventListener<TChannel,TEvent> : MonoBehaviour where TChannel : EventChannel<TEvent> where TEvent:IEvent
{
    public TChannel EventChannel;
    public UnityEvent<TEvent> Response;

    private void OnEnable()
    {
        if (EventChannel != null)
        {
            EventChannel.OnEventRaised += OnEventRaised;
        }
    }

    private void OnDisable()
    {
        if (EventChannel != null)
        {
            EventChannel.OnEventRaised -= OnEventRaised;
        }
    }

    private void OnEventRaised(TEvent eventData)
    {
        Response?.Invoke(eventData);
    }
}
