using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct FieldNode : IBufferElementData
{
    public int bestcost;
    public float2 direction;
}

public struct IslandSeed : IBufferElementData
{
    public int islandID;
    public float3 seedPosition;
}

public struct FlowField : IComponentData
{
    public int2 targetcell;
    public uint gridgeneration;
}

public struct FlowFieldRefCount : IComponentData
{
    public int value;
}

