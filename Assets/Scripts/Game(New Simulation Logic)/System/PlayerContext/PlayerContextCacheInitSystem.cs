using System;
using Unity.Burst;
using Unity.Collections;
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
        EntityCommandBuffer ecb = new EntityCommandBuffer(AllocatorManager.Temp);
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
            if (eventBus == null)
            {
                Debug.LogError("EventBus not found in Resources folder.");
                return;
            }
            var resourceChangeChannel = eventBus.GetChannel("ResourceChangeChannel") as ResourceChangeChannel;
            if (resourceChangeChannel == null)
            {
                Debug.LogError("ResourceChangeEventChannel not found in EventBus.");
                return;
            }
            resourceChangeChannel.RaiseEvent(resourcechangeEvent);
            

            var populationChangeChannel = eventBus.GetChannel("PopulationUpdatedEventChannel") as PopulationUpdatedEventChannel;
            if (populationChangeChannel == null)
            {
                Debug.LogError("PopulationUpdatedEventChannel not found in EventBus.");
                return;
            }
            PopulationUpdatedEvent populationUpdatedEvent = new PopulationUpdatedEvent(playerContextEntity.PlayerId, playerContextEntity.currentPopulation, playerContextEntity.maxPopulation);
            populationChangeChannel.RaiseEvent(populationUpdatedEvent);

            ecb.RemoveComponent<PlayerContextCachePendingTag>(entity);
        }
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
