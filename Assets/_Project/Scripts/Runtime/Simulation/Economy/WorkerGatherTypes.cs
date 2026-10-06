using Unity.Entities;
using Unity.Mathematics;

public struct DepotKey : System.IEquatable<DepotKey>
{
    public int PlayerId;
    public ResourceType ResourceType;

    public bool Equals(DepotKey other) => PlayerId == other.PlayerId && ResourceType == other.ResourceType;
    public override int GetHashCode() => (PlayerId * 397) ^ (int)ResourceType;
}

public struct DepotInfo
{
    public Entity Entity;
    public float3 Position;
    public int IslandID;
}

public struct ResourceNodeSpatialInfo
{
    public Entity Entity;
    public ResourceType Type;
    public int Amount;
    public int IslandID;
    public float3 Position;
}
