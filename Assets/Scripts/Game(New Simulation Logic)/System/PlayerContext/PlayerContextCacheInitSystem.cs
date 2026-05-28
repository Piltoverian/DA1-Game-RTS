using System;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;
partial struct PlayerContextCacheInitSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.EntityManager.CreateSingletonBuffer<PlayerContextCache>();
    }

    public void OnUpdate(ref SystemState state)
    {
        foreach (var (playerContextEntity,tag,entity) in SystemAPI.Query<PlayerContext,PlayerContextCachePendingTag>().WithEntityAccess())
        {
            var contextcache = SystemAPI.GetSingletonBuffer<PlayerContextCache>();
            PlayerContextCache cache = default(PlayerContextCache);
            bool found = false;
            for (int i = 0; i < contextcache.Length; i++)
            {
                if (contextcache[i].PlayerId == playerContextEntity.PlayerId)
                {
                    cache = contextcache[i];
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                contextcache.Add(new PlayerContextCache
                {
                    PlayerId = playerContextEntity.PlayerId,
                    age = playerContextEntity.age,
                    currentPopulation = playerContextEntity.currentPopulation,
                });
            }
            var resourcechangeEvent = new ResourceChangeEvent();
            var buffer = SystemAPI.GetBuffer<ResourcePair>(entity);
            foreach (var pair in buffer)
            {
                resourcechangeEvent.value.Add(pair);
            }
            var eventBus = Resources.Load<EventBus>("EventBus");
            var resourceChangeChannel = eventBus.GetChannel("ResourceChangeEventChannel") as ResourceChangeChannel;
            resourceChangeChannel.RaiseEvent(resourcechangeEvent);
            

            var populationChangeChannel = eventBus.GetChannel("PopulationUpdatedEventChannel") as PopulationUpdatedEventChannel;
            PopulationUpdatedEvent populationUpdatedEvent = new PopulationUpdatedEvent(playerContextEntity.PlayerId, playerContextEntity.currentPopulation, playerContextEntity.maxPopulation);
            populationChangeChannel.RaiseEvent(populationUpdatedEvent);

            state.EntityManager.RemoveComponent<PlayerContextCachePendingTag>(entity);
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
