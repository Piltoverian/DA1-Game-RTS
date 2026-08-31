using System.Collections.Generic;
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
        state.RequireForUpdate<PlayerContext>();
    }

    public void OnUpdate(ref SystemState state)
    {
        EventBus eventBus = Resources.Load<EventBus>("EventBus");
        if (eventBus == null) return;

        foreach (var (playerContext,playerContextCache, contextEntity) in SystemAPI.Query<PlayerContext, PlayerContextCache>().WithEntityAccess())
        {
            var buffer = SystemAPI.GetBuffer<ResourcePair>(contextEntity);
            var resourcescontextcache = SystemAPI.GetBuffer<ResourcePairCache>(contextEntity);
            bool flowcontrol = true; // Dùng để debug, tránh gọi event nhiều lần khi chưa fix xong logic so sánh.
            if (IsResourceChanged(buffer, ref resourcescontextcache)) flowcontrol = RaiseResourceChangeEvent(buffer, playerContext.PlayerId, eventBus);
            if (!flowcontrol) {
                Debug.Log("Something went wrong.");
                return;
            }

            if (playerContextCache.age != playerContext.age)
            {
                // Handle age change logic here
            }

            PopulationUpdatedEvent popevent = new PopulationUpdatedEvent();
            bool changed = false;
            if (playerContextCache.currentPopulation != playerContext.currentPopulation || playerContextCache.maxPopulation != playerContext.maxPopulation)
            {
                popevent.PlayerId = playerContext.PlayerId;
                popevent.CurrentPopulation = playerContext.currentPopulation;
                popevent.MaxPopulation = playerContext.maxPopulation;
                changed = true;
            }

            if (changed) {
                var populationChangeChannel = eventBus.GetChannel("PopulationUpdatedEventChannel") as PopulationUpdatedEventChannel;
                populationChangeChannel?.RaiseEvent(popevent);
            }

            playerContextCache.UpdateFromContext(playerContext);
            SystemAPI.SetComponent(contextEntity, playerContextCache);
        }
    }

    private static bool RaiseResourceChangeEvent(DynamicBuffer<ResourcePair> buffer, int playerId,EventBus eventBus)
    {
        ResourceChangeChannel channel =
            eventBus.GetChannel("ResourceChangeChannel") as ResourceChangeChannel;
        if (channel == null) return false;

        var resources = new List<ResourcePair>();
        for (int i = 0; i < buffer.Length; i++)
        {
            resources.Add(buffer[i]);
        }

        channel.RaiseEvent(new ResourceChangeEvent { playerId = playerId, value = resources });
        return true;
    }

    private static void SyncResource(
        ref DynamicBuffer<ResourcePairCache> buffer,DynamicBuffer<ResourcePair> resourceBuffer,int index
       )
    {
        buffer[index] = new ResourcePairCache
        {
            Type = resourceBuffer[index].Type,
            Amount = resourceBuffer[index].Amount
        };
    }
    
    private static bool IsResourceChanged(
        DynamicBuffer<ResourcePair> buffer,
        ref DynamicBuffer<ResourcePairCache> cacheBuffer)
    {
        bool changed = false;
        for (int i = 0; i < buffer.Length; i++)
        {
            if (i >= cacheBuffer.Length || buffer[i].Amount != cacheBuffer[i].Amount)
            {
                changed = true;
                SyncResource(ref cacheBuffer, buffer, i);
            }
        }
        return changed;
    }
}
