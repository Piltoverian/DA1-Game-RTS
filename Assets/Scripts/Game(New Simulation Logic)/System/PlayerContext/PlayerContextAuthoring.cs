using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using System.Collections.Generic;


public class PlayerContextAuthoring : MonoBehaviour
{
    public int playerId = 0;
    public int civilizationId = 0;
    public CivDef civDef;
    public string civId = "";
    public Age age = Age.Industrial;
    public List<ResourcePair> startResources;

    class Baker : Baker<PlayerContextAuthoring>
    {
        public override void Bake(PlayerContextAuthoring authoring)
        {
            if (authoring.civDef != null)
                DependsOn(authoring.civDef);

            Entity entity = GetEntity(TransformUsageFlags.None);
            FixedString64Bytes civKey = authoring.civDef != null && !string.IsNullOrEmpty(authoring.civDef.Id)
                ? new FixedString64Bytes(authoring.civDef.Id)
                : new FixedString64Bytes(authoring.civId);

            var playerContext = new PlayerContext(
                authoring.playerId,
                authoring.civilizationId,
                authoring.age,
                civKey
            );
            AddComponent(entity, playerContext);
            AddBuffer<PlayerTechnology>(entity);
            AddBuffer<PlayerPendingTech>(entity);

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

