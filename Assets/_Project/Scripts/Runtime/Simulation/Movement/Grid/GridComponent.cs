using Unity.Entities;
using Unity.Mathematics;

public struct GridComponent : IComponentData
{
    public int width;
    public int height;
    public float cellsize;
    public float3 origin;
    public uint generation;
    public uint islandGeneration;
    public bool isDirty;
    public int HeartbeatTimer;
    public int RecalcPerframe;
}

public struct GridNodeCost:IBufferElementData
{
    public int cost;
}

public struct GridIsland : IBufferElementData
{
    public int islandID;
}

public struct GridTerrain : IBufferElementData
{
    public static readonly int2[] RampDirections =
    {
        new int2( 0, -1), // 0
        new int2( 1,  0), // 1
        new int2( 0,  1), // 2
        new int2(-1,  0), // 3
    };
    public int heightLevel;
    // Shared-vertex mode: base tier plus NW/NE/SE/SW high-corner bits 1/2/4/8.
    public bool UsesVertexCorners;
    public byte CornerMask;

    public bool walkable;

    public bool isMountain;

    public bool isCliff;

    public int RampId;

    public int RampDirection;

}

public struct GridSpawnCell : IBufferElementData
{
    public int playerId;
    public int2 cell;
}
