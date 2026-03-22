using Unity.Entities;
using Unity.Mathematics;
public struct GridComponent : IComponentData
{
    public int width;
    public int height;
    public float cellsize;
    public float3 origin;
}

public struct GridNodeCost:IBufferElementData
{
    public int cost;
}

public struct GridIsland : IBufferElementData
{
    public int islandID;
}


