using System;
using System.Reflection;
using TMPro;
using Unity.Entities;
using Unity.NetCode;
using UnityEditor;
using UnityEngine;

public static class PopulationUIChecks
{
    [MenuItem("Tools/RTS/Check Population UI")]
    public static void Run()
    {
        var previous = World.DefaultGameObjectInjectionWorld;
        using var world = new World("Population UI client", WorldFlags.GameClient);
        using var server = new World("Population UI server", WorldFlags.GameServer);
        var go = new GameObject("Population UI check", typeof(RectTransform));
        var resourceTemplate = new GameObject("Resource UI template", typeof(RectTransform));
        try
        {
            // UI source must not depend on the default injection world.
            World.DefaultGameObjectInjectionWorld = server;
            var em = world.EntityManager;
            var connection = em.CreateEntity(typeof(NetworkId), typeof(ClientPlayerState));
            em.SetComponentData(connection, new NetworkId { Value = 12 });
            em.SetComponentData(connection, new ClientPlayerState { PlayerId = 7, Phase = ClientLoadingPhase.ServerReady });
            var context = em.CreateEntity(typeof(PlayerContext));
            em.SetComponentData(context, new PlayerContext { PlayerId = 7, currentPopulation = 9, maxPopulation = 25 });
            var text = go.AddComponent<TextMeshProUGUI>();
            var ui = go.AddComponent<PopText>();
            typeof(PopText).GetField("m_TextMeshPro", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, text);
            ui.OnPopChange(new PopulationUpdatedEvent(7, 8, 25));
            Check(text.text == "Pop: 8/25", "Population event must use the client's assigned ID, not serialized UI ID 0.");
            ui.OnPopChange(new PopulationUpdatedEvent(0, 100, 100));
            Check(text.text == "Pop: 8/25", "Another player's event must be ignored.");
            go.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
            Check(text.text == "Pop: 9/25", "UI must read the current population even when it missed the initial event.");
            go.SetActive(false);
            em.SetComponentData(context, new PlayerContext { PlayerId = 7, currentPopulation = 11, maxPopulation = 30 });
            go.SetActive(true);
            go.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
            Check(text.text == "Pop: 11/30", "Re-enabled UI must recover changes received while disabled.");
            var resourceText = resourceTemplate.AddComponent<TextMeshProUGUI>();
            var panel = resourceTemplate.AddComponent<InfoPanel>();
            typeof(InfoPanel).GetField("nameText", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, resourceText);
            var resourceUI = go.AddComponent<ResourceGroup>();
            resourceUI.resourceInfoPrefab = resourceTemplate;
            em.AddBuffer<ResourcePair>(context).Add(new ResourcePair(default, 150));
            go.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
            Check(go.GetComponentInChildren<InfoPanel>().GetComponent<TextMeshProUGUI>().text == "150",
                "Resource UI must read the same client context even when the default world is the server.");
            Check(UIManager.TryGetPlayer(out var source, out int id) && source == em && id == 7,
                "Both widgets must resolve the server-assigned identity in the client world.");
            Debug.Log("Population/resource UI checks passed: 6 assertions.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(resourceTemplate);
            World.DefaultGameObjectInjectionWorld = previous;
        }
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
