using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

public enum SelectionMode
{
    Click,
    Drag,
    Add,
    Remove,
    Clear
}

public struct SelectionRequest : IComponentData
{
    public SelectionMode mode;
    public int playerId;

    public float3 targetpos;
    public float3 v1;
    public float3 v2;
    public float3 v3;
    public float3 v4;

    public Unity.Physics.RaycastInput rayInput;
}