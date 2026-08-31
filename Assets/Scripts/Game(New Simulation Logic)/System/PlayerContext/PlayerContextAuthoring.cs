using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using System.Collections.Generic;


public class PlayerContextAuthoring : MonoBehaviour
{
    public int playerId = 0;
    public int civilizationId = 0;
    public Age age = Age.Industrial;
    public List<ResourcePair> startResources;

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

            var buffer = AddBuffer<ResourcePair>(entity);
            buffer.ResizeUninitialized(authoring.startResources.Count);

            for (int i = 0; i < authoring.startResources.Count; i++)
            {
                buffer[i] = authoring.startResources[i];
            }

            var contextCache = new PlayerContextCache(playerContext);

            AddComponent(entity, contextCache);

            var cacheBuffer = AddBuffer<ResourcePairCache>(entity);
            cacheBuffer.ResizeUninitialized(authoring.startResources.Count);
            for (int i = 0; i < authoring.startResources.Count; i++)
            {
                cacheBuffer[i] = new ResourcePairCache(authoring.startResources[i]);
            }

            AddComponent(entity, new PlayerContextCachePendingTag());
        }
    }
}
