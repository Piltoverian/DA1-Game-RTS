using System;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Authoring cho PlayerContext entity.
/// Đặt trên GameObject trong SubScene để Baker tạo entity chứa:
/// - PlayerContext component (PlayerId, CivilizationId, Age)
/// - DynamicBuffer ResourcePair (cache resource cho Presentation layer)
/// </summary>
public class PlayerContextAuthoring : MonoBehaviour
{
    public int playerId = 0;
    public int civilizationId = 0;
    public Age age = Age.Industrial;

    class Baker : Baker<PlayerContextAuthoring>
    {
        public override void Bake(PlayerContextAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);
            var playerContext = new PlayerContext(
                authoring.playerId,
                authoring.civilizationId,
                authoring.age
            );
            AddComponent(entity, playerContext);

            // Tạo buffer ResourcePair với size = số lượng ResourceType
            var buffer = AddBuffer<ResourcePair>(entity);
            int resourceCount = Enum.GetNames(typeof(ResourceType)).Length;
            buffer.ResizeUninitialized(resourceCount);

            // Init tất cả về 0
            var types = (ResourceType[])Enum.GetValues(typeof(ResourceType));
            for (int i = 0; i < types.Length && i < buffer.Length; i++)
            {
                buffer[i] = new ResourcePair(types[i], 0);
            }

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var cachequery = entityManager.CreateEntityQuery(typeof(DynamicBuffer<PlayerContextCache>));
            var cacheEntities = cachequery.GetSingletonBuffer<PlayerContextCache>();
            cacheEntities.Add(new PlayerContextCache(playerContext));
        }
    }
}
