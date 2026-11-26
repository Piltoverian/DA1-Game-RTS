using System;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

public static class SeedNetworkingChecks
{
    [MenuItem("Tools/RTS/Check Server Seed Boundary")]
    public static void Run()
    {
        using (var server = new World("Seed boundary server", WorldFlags.GameServer))
        {
            var em = server.EntityManager;
            var map = em.CreateEntity(typeof(MapGenerationRecipe), typeof(MapBuildState));
            em.SetComponentData(map, new MapGenerationRecipe { Seed = 40 });
            var root = em.CreateEntity(typeof(MapResourceSettings));
            em.SetComponentData(root, new MapResourceSettings { Seed = 40 });
            ServerMapSeedSystem.Initialize(em);
            uint seed = em.GetComponentData<ServerMapSeed>(map).Value;
            Check(seed != 0, "Network seed must be initialized privately on the server");
            Check(em.GetComponentData<MapGenerationRecipe>(map).Seed == seed, "Terrain must use the server runtime seed");
            Check(em.GetComponentData<MapResourceSettings>(root).Seed == seed, "Resources must use the same server map seed");
            ServerMapSeedSystem.Initialize(em);
            Check(em.GetComponentData<ServerMapSeed>(map).Value == seed, "Rejoin/update must not replace the map seed");
            var lateRoot = em.CreateEntity(typeof(MapResourceSettings));
            ServerMapSeedSystem.Initialize(em);
            Check(em.GetComponentData<MapResourceSettings>(lateRoot).Seed == seed, "Late-loaded resource settings must use the existing match seed");
        }
        using (var local = new World("Seed boundary offline", WorldFlags.Game))
        {
            var em = local.EntityManager;
            var map = em.CreateEntity(typeof(MapGenerationRecipe), typeof(MapBuildState));
            em.SetComponentData(map, new MapGenerationRecipe { Seed = 40 });
            ServerMapSeedSystem.Initialize(em);
            Check(!em.HasComponent<ServerMapSeed>(map) && em.GetComponentData<MapGenerationRecipe>(map).Seed == 40,
                "Offline reproducibility must keep its configured seed");
        }
        using (var client = new World("Seed boundary client", WorldFlags.GameClient))
        {
            var em = client.EntityManager;
            foreach (var type in ClientAuthorityDataStripSystem.AuthorityTypes)
                em.CreateEntity(ComponentType.ReadWrite(type));
            var view = em.CreateEntity(typeof(GridComponent), typeof(TerrainSyncSettings), typeof(MapResourceGhostPrefabReference));
            ClientAuthorityDataStripSystem.Strip(em);
            foreach (var type in ClientAuthorityDataStripSystem.AuthorityTypes)
            {
                using var query = em.CreateEntityQuery(ComponentType.ReadOnly(type));
                Check(query.IsEmptyIgnoreFilter, $"Client must not retain {type.Name}");
            }
            Check(em.HasComponent<GridComponent>(view) && em.HasComponent<TerrainSyncSettings>(view)
                && em.HasBuffer<MapResourceGhostPrefabReference>(view), "Client stripping must preserve render/grid/Ghost prefab dependencies");
        }
        Debug.Log("Server seed boundary checks passed.");
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
