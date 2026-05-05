using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class EventBus : MonoBehaviour
{
    [SerializeField] private List<ScriptableObject> channels;
    private static EventBus __instance;

    public static EventBus GetInstance()
    {
        if (__instance == null)
        {
            __instance = FindAnyObjectByType<EventBus>();
            if (__instance == null)
            {
                GameObject obj = new GameObject("EventBus");
                __instance = obj.AddComponent<EventBus>();
                DontDestroyOnLoad(obj);
            }
        }
        return __instance;
    }

    public void Awake()
    {
        if (__instance == null)
        {
            __instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (__instance != this)
        {
            Destroy(gameObject);
        }
    }

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
