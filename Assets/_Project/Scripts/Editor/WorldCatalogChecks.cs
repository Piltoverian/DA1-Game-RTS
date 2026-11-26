using System;
using System.Reflection;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

public static class WorldCatalogChecks
{
    [MenuItem("Tools/RTS/Check World Presentation Catalog")]
    public static void Run()
    {
        var originalDefault = World.DefaultGameObjectInjectionWorld;
        World previous = null, replacement = null, server = null;
        GameDataRegistry firstCatalog = null, secondCatalog = null;
        GameObject placerOwner = null;
        int checks = 0;
        try
        {
            firstCatalog = ScriptableObject.CreateInstance<GameDataRegistry>();
            secondCatalog = ScriptableObject.CreateInstance<GameDataRegistry>();
            previous = new World("Catalog previous local", WorldFlags.Game);
            replacement = new World("Catalog replacement client", WorldFlags.GameClient);
            server = new World("Catalog unrelated server", WorldFlags.GameServer);
            AddCatalog(previous.EntityManager, firstCatalog);
            var secondRegistry = AddCatalog(replacement.EntityManager, secondCatalog);
            AddCatalog(server.EntityManager, firstCatalog);
            World.DefaultGameObjectInjectionWorld = previous;

            Check(ReferenceEquals(RegistryAuthoring.Instance, firstCatalog),
                "Runtime catalog must resolve baked data without invoking a Baker or retaining a static SO.");
            Check(ClientPresentationCatalog.TryGet(replacement.EntityManager, out var found, out var catalog)
                && found == secondRegistry && ReferenceEquals(catalog, secondCatalog),
                "Explicit manager lookup must resolve that world's catalog even when another world is the default.");
            replacement.EntityManager.RemoveComponent<PresentationCatalogReference>(secondRegistry);
            World.DefaultGameObjectInjectionWorld = replacement;
            Check(!ClientPresentationCatalog.IsReady(replacement.EntityManager) && RegistryAuthoring.Instance == null,
                "A missing client catalog must never fall back to a local or server catalog.");
            replacement.EntityManager.AddComponentData(secondRegistry,
                new PresentationCatalogReference { Catalog = secondCatalog });
            replacement.EntityManager.RemoveComponent<RegistryPrefabElement>(secondRegistry);
            Check(!ClientPresentationCatalog.IsReady(replacement.EntityManager), "Missing prefab buffer must hold catalog readiness.");
            replacement.EntityManager.AddBuffer<RegistryPrefabElement>(secondRegistry);
            replacement.EntityManager.RemoveComponent<RegistryBlobElement>(secondRegistry);
            Check(!ClientPresentationCatalog.IsReady(replacement.EntityManager), "Missing blob buffer must hold catalog readiness.");
            replacement.EntityManager.AddBuffer<RegistryBlobElement>(secondRegistry);
            Check(ReferenceEquals(RegistryAuthoring.Instance, secondCatalog),
                "The compatibility property must refresh with the current world and restored catalog.");
            World.DefaultGameObjectInjectionWorld = server;
            Check(RegistryAuthoring.Instance == null, "Presentation compatibility lookup must reject a server-only default world.");

            var oldSelected = AddSelected(previous.EntityManager, 3);
            var newSelected = AddSelected(replacement.EntityManager, 3);
            Check(previous.SequenceNumber != replacement.SequenceNumber,
                "Fixture must cover distinct presentation world generations.");
            var oldSelection = previous.GetOrCreateSystemManaged<ClientPresentationSelectionSystem>();
            var newSelection = replacement.GetOrCreateSystemManaged<ClientPresentationSelectionSystem>();
            oldSelection.Update(); newSelection.Update();
            Check(oldSelection.FirstForPlayer(3) == oldSelected && newSelection.FirstForPlayer(3) == newSelected,
                "Selection snapshots must be built independently in each world.");
            replacement.EntityManager.SetComponentEnabled<Selected>(newSelected, false);
            newSelection.Update();
            Check(newSelection.FirstForPlayer(3) == Entity.Null && oldSelection.FirstForPlayer(3) == oldSelected,
                "Equal entity values must not carry the old world's selection into the replacement world.");

            placerOwner = new GameObject("World catalog placer check");
            placerOwner.SetActive(false);
            var placer = placerOwner.AddComponent<BuildingPlacer>();
            var refresh = typeof(BuildingPlacer).GetMethod("RefreshWorld", BindingFlags.Instance | BindingFlags.NonPublic);
            World.DefaultGameObjectInjectionWorld = previous;
            Check((bool)refresh.Invoke(placer, null), "Building placement must bind the first current catalog.");
            Set(placer, "selectedBuildingPrefab", oldSelected);
            Set(placer, "sourceWorkerEntity", oldSelected);
            Set(placer, "localPlayerId", 3);
            Set(placer, "isPlacing", true);
            World.DefaultGameObjectInjectionWorld = replacement;
            Check((bool)refresh.Invoke(placer, null)
                && (EntityManager)Get(placer, "entityManager") == replacement.EntityManager
                && (Entity)Get(placer, "selectedBuildingPrefab") == Entity.Null
                && (Entity)Get(placer, "sourceWorkerEntity") == Entity.Null
                && !(bool)Get(placer, "isPlacing") && (int)Get(placer, "localPlayerId") == -1,
                "Replacing the current world must bind its manager and cancel all old placement state.");
            var staleManager = previous.EntityManager;
            previous.Dispose();
            Check(!ClientPresentationCatalog.TryGet(staleManager, out _, out _),
                "A disposed manager must fail lookup without dereferencing the disposed world.");
            Check((bool)refresh.Invoke(placer, null), "Disposing the previous world must not break the replacement binding.");
            Debug.Log($"World presentation catalog checks passed: {checks} assertions.");
        }
        finally
        {
            if (placerOwner) UnityEngine.Object.DestroyImmediate(placerOwner);
            if (previous != null && previous.IsCreated) previous.Dispose();
            if (replacement != null && replacement.IsCreated) replacement.Dispose();
            if (server != null && server.IsCreated) server.Dispose();
            if (firstCatalog) UnityEngine.Object.DestroyImmediate(firstCatalog);
            if (secondCatalog) UnityEngine.Object.DestroyImmediate(secondCatalog);
            World.DefaultGameObjectInjectionWorld = originalDefault != null && originalDefault.IsCreated ? originalDefault : null;
        }

        void Check(bool condition, string message)
        {
            checks++;
            if (!condition) throw new InvalidOperationException(message);
        }
    }

    static Entity AddCatalog(EntityManager em, GameDataRegistry catalog)
    {
        var entity = em.CreateEntity(typeof(GameDataRegistryComponent), typeof(PresentationCatalogReference));
        em.SetComponentData(entity, new PresentationCatalogReference { Catalog = catalog });
        em.AddBuffer<RegistryBlobElement>(entity);
        em.AddBuffer<RegistryPrefabElement>(entity);
        em.AddBuffer<RegistryTechEffectElement>(entity);
        return entity;
    }

    static Entity AddSelected(EntityManager em, int playerId)
    {
        var entity = em.CreateEntity(typeof(Selected), typeof(Selectable), typeof(EntityOwner));
        em.SetComponentData(entity, new EntityOwner { PlayerID = playerId });
        return entity;
    }

    static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
