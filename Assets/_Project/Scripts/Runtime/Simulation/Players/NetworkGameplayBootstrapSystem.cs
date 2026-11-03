using System;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
[UpdateInGroup(typeof(InitializationSystemGroup))]
[UpdateBefore(typeof(MatchStateInitSystem))]
public partial class NetworkGameplayBootstrapSystem : SystemBase
{
    protected override void OnCreate() { }

    protected override void OnUpdate()
    {
        bool server = World.IsServer();
        if (!SystemAPI.TryGetSingleton<NetworkGameplayPrefabs>(out var prefabs) && server) return;
        if (server && !SystemAPI.HasSingleton<MatchSession>())
        {
            var match = EntityManager.Instantiate(prefabs.Match);
            EntityManager.SetComponentData(match, new MatchSession { Id = (ulong)DateTime.UtcNow.Ticks });
        }

        using var query = EntityManager.CreateEntityQuery(typeof(PlayerContextSeed), typeof(PlayerResourceSeed));
        using var seeds = query.ToEntityArray(Allocator.Temp);
        foreach (var seedEntity in seeds)
        {
            var seed = EntityManager.GetComponentData<PlayerContextSeed>(seedEntity);
            if (seed.Instance != Entity.Null && EntityManager.Exists(seed.Instance)) continue;
            var instance = server ? EntityManager.Instantiate(prefabs.PlayerContext) : EntityManager.CreateEntity(
                typeof(PlayerContext), typeof(PlayerContextCache), typeof(PlayerContextCachePendingTag),
                typeof(ResourcePair), typeof(ResourcePairCache), typeof(PlayerTechnology), typeof(PlayerPendingTech));
            EntityManager.SetComponentData(instance, seed.Value);
            EntityManager.SetComponentData(instance, new PlayerContextCache(seed.Value));
            var resources = EntityManager.GetBuffer<PlayerResourceSeed>(seedEntity, true);
            var target = EntityManager.GetBuffer<ResourcePair>(instance);
            var cache = EntityManager.GetBuffer<ResourcePairCache>(instance);
            foreach (var resource in resources)
            {
                target.Add(resource.Value);
                cache.Add(new ResourcePairCache(resource.Value));
            }
            seed.Instance = instance;
            EntityManager.SetComponentData(seedEntity, seed);
        }

        using var slotsQuery = EntityManager.CreateEntityQuery(typeof(PlayerBootstrapSlot));
        if (slotsQuery.CalculateEntityCount() != 1) return;
        var slots = EntityManager.GetBuffer<PlayerBootstrapSlot>(slotsQuery.GetSingletonEntity());
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (!EntityManager.HasComponent<PlayerContextSeed>(slot.Context)) continue;
            var instance = EntityManager.GetComponentData<PlayerContextSeed>(slot.Context).Instance;
            if (instance == Entity.Null) continue;
            slot.Context = instance;
            slots[i] = slot;
        }
        if (server && SystemAPI.TryGetSingletonEntity<MatchSession>(out var matchEntity))
        {
            var roster = EntityManager.GetBuffer<MatchPlayerRoster>(matchEntity);
            if (roster.Length == 0)
                foreach (var slot in slots)
                {
                    var context = EntityManager.GetComponentData<PlayerContext>(slot.Context);
                    roster.Add(new MatchPlayerRoster { PlayerId = context.PlayerId, TeamId = context.TeamID, CivId = context.CivID });
                }
        }
    }
}
