using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

public struct NetworkGameplayPrefabs : IComponentData
{
    public Entity PlayerContext;
    public Entity Match;
}

public struct PlayerContextSeed : IComponentData
{
    public PlayerContext Value;
    public Entity Instance;
}

public struct PlayerResourceSeed : IBufferElementData
{
    public ResourcePair Value;
}

[GhostComponent(OwnerSendType = SendToOwnerType.SendToOwner)]
public struct PlayerContextSession : IComponentData
{
    [GhostField] public ulong MatchSessionId;
    [GhostField] public uint JoinGeneration;
}

public class PlayerContextGhostAuthoring : MonoBehaviour
{
    class Baker : Baker<PlayerContextGhostAuthoring>
    {
        public override void Bake(PlayerContextGhostAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent<CustomGhostRelevancy>(entity);
            AddComponent(entity, new PlayerContext { PlayerId = -1 });
            AddComponent<PlayerContextSession>(entity);
            AddComponent<PlayerContextCache>(entity);
            AddComponent<PlayerContextCachePendingTag>(entity);
            AddBuffer<ResourcePair>(entity);
            AddBuffer<ResourcePairCache>(entity);
            AddBuffer<PlayerTechnology>(entity);
            AddBuffer<PlayerPendingTech>(entity);
        }
    }
}
