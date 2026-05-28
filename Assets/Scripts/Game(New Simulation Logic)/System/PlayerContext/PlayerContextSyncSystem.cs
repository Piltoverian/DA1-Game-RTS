using System.Collections.Generic;
using System.Security.Cryptography;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// ECS System chạy ở LateSimulationSystemGroup.
/// Đọc PlayerResourceData → cập nhật vào PlayerContext (DynamicBuffer ResourcePair) → 
/// so sánh với giá trị cũ, nếu thay đổi thì fire ResourceChangeEvent qua EventBus.
/// PlayerContext đóng vai trò cache/bridge DTO giữa Simulation và Presentation.
/// KHÔNG dùng BurstCompile vì cần truy cập managed object (EventBus, ScriptableObject).
/// </summary>
/// 
[UpdateAfter(typeof(PlayerContextCacheInitSystem))]
[UpdateInGroup(typeof(LateSimulationSystemGroup))]
public partial struct PlayerContextSyncSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        
        state.RequireForUpdate<PlayerResourceData>();
        state.RequireForUpdate<PlayerContext>();
    }

    public void OnUpdate(ref SystemState state)
    {
        foreach (var playerContextEntity in SystemAPI.Query<PlayerContext>())
        {

            PlayerResourceData res = SystemAPI.GetSingleton<PlayerResourceData>();

            Entity contextEntity = SystemAPI.GetSingletonEntity<PlayerContext>();
            var buffer = SystemAPI.GetBuffer<ResourcePair>(contextEntity);

            EventBus eventBus = Resources.Load<EventBus>("EventBus");
            if (eventBus == null) return;
            bool flowcontrol = true; // Dùng để debug, tránh gọi event nhiều lần khi chưa fix xong logic so sánh.
            if (IsResourceChanged(buffer, res)) flowcontrol = RaiseResourceChangeEvent(buffer, eventBus);
            if (!flowcontrol) {
                Debug.Log("Something went wrong.");
                return;
            }
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
            if (!found) { Debug.LogWarning("PlayerContextCache not found for player: " + playerContextEntity.PlayerId); }

           

            if (cache.age != playerContextEntity.age)
            {
                // Handle age change logic here

            }

            PopulationUpdatedEvent popevent=new PopulationUpdatedEvent();
            bool changed = false;
            if (cache.currentPopulation != playerContextEntity.currentPopulation|| cache.maxPopulation != playerContextEntity.maxPopulation)
            {
                popevent.CurrentPopulation = playerContextEntity.currentPopulation;
                popevent.MaxPopulation = playerContextEntity.maxPopulation;
                changed = true;
            }

            if (changed) {
                var populationChangeChannel = eventBus.GetChannel("PopulationUpdatedEventChannel") as PopulationUpdatedEventChannel;
                populationChangeChannel.RaiseEvent(popevent);
            }
            cache.UpdateFromContext(playerContextEntity);
        }
    }

    private static bool RaiseResourceChangeEvent(DynamicBuffer<ResourcePair> buffer, EventBus eventBus)
    {
        ResourceChangeChannel channel =
            eventBus.GetChannel("ResourceChangeChannel") as ResourceChangeChannel;
        if (channel == null) return false;

        var resources = new List<ResourcePair>();
        for (int i = 0; i < buffer.Length; i++)
        {
            resources.Add(buffer[i]);
        }

        channel.RaiseEvent(new ResourceChangeEvent { value = resources });
        return true;
    }

    private static bool SyncResource(
        ref DynamicBuffer<ResourcePair> buffer,
        ResourceType type,
        int newAmount)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            if (buffer[i].Type == type)
            {
                if ((int)buffer[i].Amount == newAmount)
                    return false;

                buffer[i] = new ResourcePair(type, newAmount);
                return true;
            }
        }

        buffer.Add(new ResourcePair(type, newAmount));
        return true;
    }
    

    private static bool IsResourceChanged(
        DynamicBuffer<ResourcePair> buffer,
        PlayerResourceData res)
    {
        bool changed = false;
        changed |= SyncResource(ref buffer, ResourceType.Gold, res.Gold);
        changed |= SyncResource(ref buffer, ResourceType.Wood, res.Wood);
        changed |= SyncResource(ref buffer, ResourceType.Food, res.Food);
        return changed;
    }
}
