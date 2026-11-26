using System;
using System.Security.Cryptography;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

// A prefab dependency only; client builds need resource Ghost types, not placement recipes.
public struct MapResourceGhostPrefabReference : IBufferElementData { public Entity Value; }

// Created at runtime on the authoritative map entity. Never baked or replicated.
public struct ServerMapSeed : IComponentData { public uint Value; }
public struct ServerMapGenerated : IComponentData { }
public struct ResourceMapSeedApplied : IComponentData { }

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(InitializationSystemGroup))]
[UpdateBefore(typeof(MapTerrainGenerationSystem))]
public partial class ServerMapSeedSystem : SystemBase
{
    protected override void OnUpdate() => Initialize(EntityManager);

    public static void Initialize(EntityManager em)
    {
        if (!em.World.IsServer()) return;
        using var maps = em.CreateEntityQuery(typeof(MapGenerationRecipe), typeof(MapBuildState));
        using var entities = maps.ToEntityArray(Allocator.Temp);
        foreach (var entity in entities)
        {
            if (em.HasComponent<ServerMapSeed>(entity)) continue;
            // The configured seed is for offline reproducibility only. A network match
            // always replaces it before the generator can read it.
            uint seed;
            using (var random = RandomNumberGenerator.Create())
            {
                var bytes = new byte[sizeof(uint)];
                do { random.GetBytes(bytes); seed = BitConverter.ToUInt32(bytes, 0); } while (seed == 0);
            }
            var recipe = em.GetComponentData<MapGenerationRecipe>(entity);
            recipe.Seed = seed;
            em.SetComponentData(entity, recipe);
            em.AddComponentData(entity, new ServerMapSeed { Value = seed });
        }
        using var seedQuery = em.CreateEntityQuery(typeof(ServerMapSeed));
        if (seedQuery.CalculateEntityCount() != 1) return;
        uint mapSeed = seedQuery.GetSingleton<ServerMapSeed>().Value;
        using var resources = em.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadWrite<MapResourceSettings>() },
            None = new[] { ComponentType.ReadOnly<ResourceMapSeedApplied>() }
        });
        using var roots = resources.ToEntityArray(Allocator.Temp);
        foreach (var root in roots)
        {
            var settings = em.GetComponentData<MapResourceSettings>(root);
            settings.Seed = mapSeed;
            em.SetComponentData(root, settings);
            em.AddComponent<ResourceMapSeedApplied>(root);
        }
    }
}

// Combined Editor/host entity scenes serve both worlds. Pure-client baking omits
// these types; this extra guard keeps the client world free of authority state.
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
[UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
public partial class ClientAuthorityDataStripSystem : SystemBase
{
    protected override void OnUpdate() => Strip(EntityManager);

    public static void Strip(EntityManager em)
    {
        if (!em.World.IsClient() && !em.World.IsThinClient()) return;
        foreach (var type in AuthorityTypes) Remove(em, type);
    }

    public static readonly Type[] AuthorityTypes =
    {
        typeof(MapGenerationRecipe), typeof(MapBuildState), typeof(GridSpawnCell),
        typeof(ServerMapSeed), typeof(ServerMapGenerated), typeof(ResourceMapSeedApplied), typeof(MapResourceSettings),
        typeof(MapResourcePrefab), typeof(MapResourceReport), typeof(PlayerBootstrapState),
        typeof(PlayerBootstrapSlot), typeof(PlayerBootstrapSpawned), typeof(PlayerContextSeed),
        typeof(PlayerResourceSeed), typeof(PlayerJoinTimeout)
    };

    static void Remove(EntityManager em, Type type)
    {
        using var query = em.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly(type) },
            Options = EntityQueryOptions.IncludeDisabledEntities
        });
        if (!query.IsEmptyIgnoreFilter) em.RemoveComponent(query, ComponentType.ReadWrite(type));
    }
}
