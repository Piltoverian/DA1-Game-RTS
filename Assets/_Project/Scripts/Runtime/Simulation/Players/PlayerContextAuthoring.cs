using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using System.Collections.Generic;


public class PlayerContextAuthoring : MonoBehaviour
{
    public int playerId = 0;
    public CivDef civDef;
    [Tooltip("Bind to the player created by Map Settings instead of creating another context.")]
    public bool useBootstrapContext;
    public Age age = Age.Industrial;
    public List<ResourcePair> startResources = new();

    class Baker : Baker<PlayerContextAuthoring>
    {
        public override void Bake(PlayerContextAuthoring authoring)
        {
            if (authoring.civDef == null)
                throw new InvalidOperationException("PlayerContextAuthoring requires a CivDef (for example TempCiv).");
            DependsOn(authoring.civDef);
            string id = authoring.civDef.Id;
            if (string.IsNullOrWhiteSpace(id) || System.Text.Encoding.UTF8.GetByteCount(id) > FixedString64Bytes.UTF8MaxLengthInBytes)
                throw new InvalidOperationException("CivDef.Id must be non-empty and fit in FixedString64Bytes.");
            if (authoring.playerId < 0)
                throw new InvalidOperationException("Player ID must be non-negative.");

            Entity entity = GetEntity(TransformUsageFlags.None);
            FixedString64Bytes civKey = new FixedString64Bytes(id);
            if (authoring.useBootstrapContext)
            {
                AddComponent(entity, new PlayerContextBinding { PlayerId = authoring.playerId, CivID = civKey });
                return;
            }

            var playerContext = new PlayerContext(
                authoring.playerId,
                0,
                authoring.age,
                civKey
            );
            AddComponent(entity, playerContext);
            AddBuffer<PlayerTechnology>(entity);
            AddBuffer<PlayerPendingTech>(entity);

            var buffer = AddBuffer<ResourcePair>(entity);
            var resources = authoring.startResources ?? new List<ResourcePair>();
            buffer.ResizeUninitialized(resources.Count);

            for (int i = 0; i < resources.Count; i++)
            {
                buffer[i] = resources[i];
            }

            var contextCache = new PlayerContextCache(playerContext);

            AddComponent(entity, contextCache);

            var cacheBuffer = AddBuffer<ResourcePairCache>(entity);
            cacheBuffer.ResizeUninitialized(resources.Count);
            for (int i = 0; i < resources.Count; i++)
            {
                cacheBuffer[i] = new ResourcePairCache(resources[i]);
            }

            AddComponent(entity, new PlayerContextCachePendingTag());
        }
    }
}

