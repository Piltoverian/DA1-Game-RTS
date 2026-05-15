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
        PlayerResourceData res = SystemAPI.GetSingleton<PlayerResourceData>();

        Entity contextEntity = SystemAPI.GetSingletonEntity<PlayerContext>();
        var buffer = SystemAPI.GetBuffer<ResourcePair>(contextEntity);

        bool changed = false;

        changed |= SyncResource(ref buffer, ResourceType.Gold, res.Gold);
        changed |= SyncResource(ref buffer, ResourceType.Wood, res.Wood);
        changed |= SyncResource(ref buffer, ResourceType.Food, res.Food);

        if (!changed) return;

        EventBus eventBus = Resources.Load<EventBus>("EventBus");
        if (eventBus == null) return;

        ResourceChangeChannel channel =
            eventBus.GetChannel("ResourceChangeChannel") as ResourceChangeChannel;
        if (channel == null) return;

        var resources = new List<ResourcePair>();
        for (int i = 0; i < buffer.Length; i++)
        {
            resources.Add(buffer[i]);
        }

        channel.RaiseEvent(new ResourceChangeEvent { value = resources });
    }

    /// <summary>
    /// So sánh giá trị resource trong PlayerContext buffer với giá trị mới từ PlayerResourceData.
    /// Nếu khác → cập nhật buffer và return true.
    /// Nếu chưa có entry cho type → thêm mới.
    /// </summary>
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
}
