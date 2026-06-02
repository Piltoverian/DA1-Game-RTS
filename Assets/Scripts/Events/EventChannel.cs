using UnityEngine;
using UnityEngine.Events;

public abstract class EventChannel<T> : ScriptableObject where T:IEvent
{
   public UnityAction<T> OnEventRaised;
   public void RaiseEvent(T eventData)
   {
       OnEventRaised?.Invoke(eventData);
   }
}

public interface IEvent
{
   // Marker interface for events
}