using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Unity.NetCode;
using Unity.NetCode.Hybrid;

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
// One roster and one balance configuration for the entire match.
public class PlayerBootstrapAuthoring : MonoBehaviour
{
    [Serializable]
    public class PlayerEntry
    {
        [Min(0)] public int PlayerId;
        [Min(-1)] public int TeamID = -1;
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
                settings.StartingResources == null || authoring.Players == null || authoring.Players.Count == 0 ||
                !float.IsFinite(settings.JoinTimeoutSeconds) || settings.JoinTimeoutSeconds < 0 ||
                !float.IsFinite(settings.LoadingTimeoutSeconds) || settings.LoadingTimeoutSeconds <= 0)
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
            bool client = this.GetNetcodeTarget(false) == NetcodeConversionTarget.Client;
            if (!client)
            {
                AddComponent(root, new PlayerBootstrapState
                {
                    WorkerCount = settings.StartingWorkerCount,
                    PlacementRadiusCells = settings.PlacementRadiusCells
                });
                AddComponent(root, new PlayerJoinTimeout { Seconds = settings.JoinTimeoutSeconds, LoadingSeconds = settings.LoadingTimeoutSeconds });
                AddBuffer<PlayerBootstrapSlot>(root);
                AddBuffer<PlayerBootstrapSpawned>(root);
            }
            var playerGhost = Resources.Load<GameObject>("Networking/PlayerContextGhost");
            var matchGhost = Resources.Load<GameObject>("Networking/MatchGhost");
            if (!playerGhost || !matchGhost) throw new InvalidOperationException("Networking ghost prefabs are missing.");
            DependsOn(playerGhost); DependsOn(matchGhost);
            AddComponent(root, new NetworkGameplayPrefabs
            {
                PlayerContext = GetEntity(playerGhost, TransformUsageFlags.None),
                Match = GetEntity(matchGhost, TransformUsageFlags.None)
            });
            if (authoring.MapConfig)
            {
                var config = authoring.MapConfig.Resources;
                if (config == null) throw new InvalidOperationException("Map resource config must not be null.");
                var ghostPrefabs = AddBuffer<MapResourceGhostPrefabReference>(root);
                var prefabs = client ? default : AddBuffer<MapResourcePrefab>(root);
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
                        var prefab = GetEntity(definition.Prefab, TransformUsageFlags.Dynamic);
                        ghostPrefabs.Add(new MapResourceGhostPrefabReference { Value = prefab });
                        if (!client) prefabs.Add(new MapResourcePrefab { Value = prefab,
                            Id = new FixedString64Bytes(definition.Id), Type = definition.Type, Tier = definition.Tier,
                            AllowedClusters = definition.AllowedClusters, Weight = definition.Weight, AmountPerMine = definition.AmountPerMine });
                    }
                    if (!client)
                    {
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
            }
            if (client) return;
            foreach (var player in authoring.Players)
            {
                DependsOn(player.Civilization);
                Entity contextEntity = CreateAdditionalEntity(TransformUsageFlags.None);
                var context = new PlayerContext(player.PlayerId, 0, settings.StartingAge, new FixedString64Bytes(player.Civilization.Id), player.TeamID);
                AddComponent(contextEntity, new PlayerContextSeed { Value = context });
                var starting = AddBuffer<PlayerResourceSeed>(contextEntity);
                foreach (var resource in settings.StartingResources)
                    starting.Add(new PlayerResourceSeed { Value = resource });
                AppendToBuffer(root, new PlayerBootstrapSlot { PlayerId = player.PlayerId, SpawnSlot = player.SpawnSlot, Context = contextEntity });
            }
        }

        static bool ValidRange(Vector2 range) => float.IsFinite(range.x) && float.IsFinite(range.y) && range.x >= 0 && range.y >= range.x && range.y <= 1;
    }
}



