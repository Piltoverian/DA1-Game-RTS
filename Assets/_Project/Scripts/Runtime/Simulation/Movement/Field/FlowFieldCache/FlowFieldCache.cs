using Unity.Entities;
using Unity.Mathematics;

public struct FlowFieldCache : IComponentData
{

}

public struct FlowFieldCacheEntry : IBufferElementData
{
    public int2 targetCell;
    public Entity flowField;
    public uint lastUsedFrame;
}

