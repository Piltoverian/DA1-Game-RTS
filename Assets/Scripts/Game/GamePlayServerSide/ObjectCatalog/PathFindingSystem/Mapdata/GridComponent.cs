using Unity.Entities;
using Unity.Mathematics;
public struct GridComponent : IComponentData
{
    public int width;
    public int height;
    public float cellsize;
    public float3 origin;
}

public struct GridNode : IBufferElementData
{
    public int bestcost;
    public float2 direction;
}

public struct GridNodeCost:IBufferElementData
{
    public int cost;
}

public struct Target:IComponentData
{
    public float3 worldpos;
}