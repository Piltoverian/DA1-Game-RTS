using UnityEngine;

[CreateAssetMenu(fileName = "MapGenConfig", menuName = "Game/Map Gen Config")]
public class MapGenConfig : ScriptableObject
{
    public GridAuthoring.MapType MapSize = GridAuthoring.MapType.Extreme;
    [Min(.01f)] public float MinimumCellSize = 1;
    public bool GenerateTerrain = true;
    public TerrainGenerationSettings Terrain = new TerrainGenerationSettings();
    public PlayerBootstrapSettings PlayerBootstrap;
    public MapResourceConfig Resources = new MapResourceConfig();
}

[System.Serializable]
public class MapResourceConfig
{
    public bool Enabled = true;
    public System.Collections.Generic.List<MapResourceDefinition> Catalog = new();
    [Min(1)] public int ClusterMin = 2;
    [Min(1)] public int ClusterMax = 4;
    [Min(0)] public int MainClusters = 1;
    [Min(0)] public int SecondaryClusters = 1;
    [Min(0)] public int AdvantageClusters = 1;
    [Min(0)] public int ContestedClusters = 1;
    public MainClusterConfig Main = new MainClusterConfig();
    [Tooltip("X=min, Y=max, multiplied by map width in cells; distance from player base.")]
    public Vector2 SecondaryRange = new Vector2(.11f, .22f);
    [Tooltip("X=min, Y=max, multiplied by map width in cells; distance from player base.")]
    public Vector2 AdvantageRange = new Vector2(.23f, .40f);
    [Tooltip("X=min, Y=max, multiplied by map width in cells; distance from map center within a player sector.")]
    public Vector2 ContestedRange = new Vector2(.07f, .28f);
    [Min(1)] public float MinimumClusterRadius = 4;
    [Min(.1f)] public float RadiusPerSqrtCount = 2.3f;
    [Min(0)] public float ClusterGap = 3;
    [Min(1)] public float MemberSpacing = 3;
    [Min(1)] public int CenterAttempts = 100;
    [Min(1)] public int MemberAttempts = 160;
}

public struct MapResourceSettings : Unity.Entities.IComponentData
{
    public uint Seed;
    public int ClusterMin, ClusterMax;
    public Unity.Mathematics.int4 Quotas;
    public float MainDistanceCells;
    public int MainMineCount;
    public Unity.Mathematics.float2 SecondaryRange, AdvantageRange, ContestedRange;
    public float MinimumClusterRadius, RadiusPerSqrtCount, ClusterGap, MemberSpacing;
    public int CenterAttempts, MemberAttempts;
}

public struct MapResourcePrefab : Unity.Entities.IBufferElementData
{
    public Unity.Entities.Entity Value;
    public Unity.Collections.FixedString64Bytes Id;
    public ResourceType Type;
    public ResourceValueTier Tier;
    public ResourceClusterMask AllowedClusters;
    public int Weight;
    public int AmountPerMine;
}

public enum ResourceValueTier : byte { Common, Rich, Premium }

[System.Flags]
public enum ResourceClusterMask : byte { Main = 1, Secondary = 2, Advantage = 4, Contested = 8, All = 15 }

[System.Serializable]
public class MapResourceDefinition
{
    public string Id;
    public ResourceType Type;
    public GameObject Prefab;
    [Tooltip("Common: any allowed role. Rich: advantage/contested. Premium: contested only.")]
    public ResourceValueTier Tier;
    public ResourceClusterMask AllowedClusters = ResourceClusterMask.All;
    [Tooltip("Relative selection frequency within the chosen tier; not stock or income.")]
    [Min(1)] public int Weight = 1;
    [Tooltip("Required stock for each spawned mine. Prefabs do not contain a stock setting.")]
    [UnityEngine.Serialization.FormerlySerializedAs("AmountOverride")]
    [Min(1)] public int AmountPerMine = 1500;
}

public struct MapResourceIdentity : Unity.Entities.IComponentData
{
    public Unity.Collections.FixedString64Bytes DefinitionId;
    public ResourceValueTier Tier;
    public int ClusterRole;
}

public struct MapResourceReport : Unity.Entities.IComponentData
{
    public int RequestedClusters, PlacedClusters, Nodes, MainPlacedClusters, MainNodes;
}

[System.Serializable]
public class MainClusterConfig
{
    [Min(1)] public float DistanceCells = 12;
    [Min(1)] public int MinesPerCluster = 3;
}

