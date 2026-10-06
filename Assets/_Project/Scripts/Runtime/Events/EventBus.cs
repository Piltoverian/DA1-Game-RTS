using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EventBus", menuName = "Events/EventBus")]
public class EventBus : ScriptableObject
{
    [SerializeField] private List<ScriptableObject> channels;

    public ScriptableObject GetChannel(string name)
    {
        name = name.Normalize();
        foreach (var channel in channels)
        {
            if (channel.name.Normalize() == name)
            {
                return channel;
            }
        }
        return null;
    }
}
