using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public enum PlayerBootstrapPhase : byte { Waiting, Finalizing, Ready, Failed }

public struct PlayerBootstrapState : IComponentData
{
    public PlayerBootstrapPhase Phase;
    public int WorkerCount;
    public int PlacementRadiusCells;
    public uint GridGeneration;
}

public struct PlayerBootstrapSlot : IBufferElementData
{
    public int PlayerId;
    public int SpawnSlot;
    public Entity Context;
}

public struct PlayerBootstrapSpawned : IBufferElementData { public Entity Value; }
public struct PlayerBootstrapResource : IBufferElementData
{
    public Entity Prefab;
    public int CountPerPlayer;
    public int MinDistanceCells;
    public int AmountPerMine;
}

// One roster and one balance configuration for the entire match.
public class PlayerBootstrapAuthoring : MonoBehaviour
{
    [Serializable]
    public class PlayerEntry
    {
        [Min(0)] public int PlayerId;
        [Min(0)] public int SpawnSlot;
        public CivDef Civilization;
    }

    public MapGenConfig MapConfig;
    public PlayerBootstrapSettings Settings;
    public List<PlayerEntry> Players = new();

    class Baker : Baker<PlayerBootstrapAuthoring>
    {
        public override void Bake(PlayerBootstrapAuthoring authoring)
        {
            if (authoring.MapConfig) DependsOn(authoring.MapConfig);
            var settings = authoring.MapConfig ? authoring.MapConfig.PlayerBootstrap : authoring.Settings;
            if (settings == null) throw new InvalidOperationException("Player bootstrap requires shared Settings.");
            DependsOn(settings);

            if (settings.StartingWorkerCount < 0 || settings.PlacementRadiusCells < 1 ||
                settings.StartingResources == null || authoring.Players == null || authoring.Players.Count == 0)
                throw new InvalidOperationException("Invalid player bootstrap settings or empty roster.");
            var resources = new HashSet<ResourceType>();
            foreach (var resource in settings.StartingResources)
                if (!resources.Add(resource.Type) || float.IsNaN(resource.Amount) || float.IsInfinity(resource.Amount) || resource.Amount < 0)
                    throw new InvalidOperationException("Starting resources must have unique types and finite non-negative amounts.");
            var ids = new HashSet<int>();
            var slots = new HashSet<int>();
            foreach (var player in authoring.Players)
                if (player == null || player.PlayerId < 0 || player.SpawnSlot < 0 || player.Civilization == null ||
                    string.IsNullOrWhiteSpace(player.Civilization.Id) || !ids.Add(player.PlayerId) || !slots.Add(player.SpawnSlot))
                    throw new InvalidOperationException("Each player requires a unique ID, unique spawn slot, and civilization.");

            Entity root = GetEntity(TransformUsageFlags.None);
            AddComponent(root, new PlayerBootstrapState
            {
                WorkerCount = settings.StartingWorkerCount,
                PlacementRadiusCells = settings.PlacementRadiusCells
            });
            AddBuffer<PlayerBootstrapSlot>(root);
            AddBuffer<PlayerBootstrapSpawned>(root);
            AddBuffer<PlayerBootstrapResource>(root);
            if (authoring.MapConfig)
            {
                var config = authoring.MapConfig.Resources;
                if (config == null) throw new InvalidOperationException("Map resource config must not be null.");
                var prefabs = AddBuffer<MapResourcePrefab>(root);
                if (config.Enabled)
                {
                    if (config.Catalog == null || config.Catalog.Count == 0 || config.ClusterMin < 1 || config.ClusterMax < config.ClusterMin ||
                        config.MainClusters < 0 || config.SecondaryClusters < 0 || config.AdvantageClusters < 0 || config.ContestedClusters < 0 ||
                        config.CenterAttempts < 1 || config.MemberAttempts < config.ClusterMax || config.MinimumClusterRadius < 1 ||
                        config.RadiusPerSqrtCount <= 0 || config.ClusterGap < 0 || config.MemberSpacing < 1 ||
                        config.Main == null || !float.IsFinite(config.Main.DistanceCells) || config.Main.DistanceCells < 1 || config.Main.MinesPerCluster < 1 || config.MemberAttempts < config.Main.MinesPerCluster ||
                        !ValidRange(config.SecondaryRange) || !ValidRange(config.AdvantageRange) || !ValidRange(config.ContestedRange))
                        throw new InvalidOperationException("Invalid map resource cluster config.");
                    var resourceIds = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var definition in config.Catalog)
                    {
                        if (definition == null || !definition.Prefab || string.IsNullOrWhiteSpace(definition.Id) ||
                            System.Text.Encoding.UTF8.GetByteCount(definition.Id) > 61 || !resourceIds.Add(definition.Id) ||
                            definition.Weight < 1 || definition.AmountPerMine < 1 || (int)definition.Tier > 2 ||
                            definition.AllowedClusters == 0 || ((int)definition.AllowedClusters & ~15) != 0)
                            throw new InvalidOperationException("Resource catalog requires unique IDs, prefab, valid tier/roles, positive weight and positive configured stock.");
                        var resourceAuthoring = definition.Prefab.GetComponent<ResourceAuthoring>();
                        if (!resourceAuthoring || resourceAuthoring.ResourceType != definition.Type)
                            throw new InvalidOperationException($"Resource '{definition.Id}' type must match ResourceAuthoring on its prefab.");
                        DependsOn(resourceAuthoring);
                        prefabs.Add(new MapResourcePrefab { Value = GetEntity(definition.Prefab, TransformUsageFlags.Dynamic),
                            Id = new FixedString64Bytes(definition.Id), Type = definition.Type, Tier = definition.Tier,
                            AllowedClusters = definition.AllowedClusters, Weight = definition.Weight, AmountPerMine = definition.AmountPerMine });
                    }
                    AddComponent(root, new MapResourceSettings {
                        Seed = authoring.MapConfig.Terrain.seed,
                        ClusterMin = config.ClusterMin, ClusterMax = config.ClusterMax,
                        Quotas = new Unity.Mathematics.int4(config.MainClusters, config.SecondaryClusters, config.AdvantageClusters, config.ContestedClusters),
                        MainDistanceCells = config.Main.DistanceCells, MainMineCount = config.Main.MinesPerCluster,
                        SecondaryRange = config.SecondaryRange, AdvantageRange = config.AdvantageRange, ContestedRange = config.ContestedRange,
                        MinimumClusterRadius = config.MinimumClusterRadius, RadiusPerSqrtCount = config.RadiusPerSqrtCount,
                        ClusterGap = config.ClusterGap, MemberSpacing = config.MemberSpacing,
                        CenterAttempts = config.CenterAttempts, MemberAttempts = config.MemberAttempts });
                    AddComponent<MapResourceReport>(root);
                }
            }
            if (settings.StartingNodes == null) throw new InvalidOperationException("StartingNodes must not be null.");
            // A map config owns map resources; legacy bootstrap nodes apply only without one.
            foreach (var node in authoring.MapConfig ? new List<PlayerBootstrapSettings.ResourceEntry>() : settings.StartingNodes)
            {
                if (node == null || node.Prefab == null || node.CountPerPlayer < 1 || node.MinDistanceCells < 1 || node.AmountPerMine < 1 ||
                    node.MinDistanceCells >= settings.PlacementRadiusCells)
                    throw new InvalidOperationException("Invalid starting resource prefab/count/distance.");
                AppendToBuffer(root, new PlayerBootstrapResource
                {
                    Prefab = GetEntity(node.Prefab, TransformUsageFlags.Dynamic),
                    CountPerPlayer = node.CountPerPlayer, MinDistanceCells = node.MinDistanceCells, AmountPerMine = node.AmountPerMine
                });
            }
            foreach (var player in authoring.Players)
            {
                DependsOn(player.Civilization);
                Entity contextEntity = CreateAdditionalEntity(TransformUsageFlags.None);
                var context = new PlayerContext(player.PlayerId, 0, settings.StartingAge, new FixedString64Bytes(player.Civilization.Id));
                AddComponent(contextEntity, context);
                AddComponent(contextEntity, new PlayerContextCache(context));
                AddComponent<PlayerContextCachePendingTag>(contextEntity);
                AddBuffer<PlayerTechnology>(contextEntity);
                AddBuffer<PlayerPendingTech>(contextEntity);
                AddBuffer<ResourcePair>(contextEntity);
                AddBuffer<ResourcePairCache>(contextEntity);
                foreach (var resource in settings.StartingResources)
                {
                    AppendToBuffer(contextEntity, resource);
                    AppendToBuffer(contextEntity, new ResourcePairCache(resource));
                }
                AppendToBuffer(root, new PlayerBootstrapSlot { PlayerId = player.PlayerId, SpawnSlot = player.SpawnSlot, Context = contextEntity });
            }
        }

        static bool ValidRange(Vector2 range) => float.IsFinite(range.x) && float.IsFinite(range.y) && range.x >= 0 && range.y >= range.x && range.y <= 1;
    }
}



