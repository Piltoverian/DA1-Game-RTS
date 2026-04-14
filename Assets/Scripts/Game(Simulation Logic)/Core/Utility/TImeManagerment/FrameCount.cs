using Unity.Entities;

public struct FrameCount : IComponentData
{
    public int value;
}

public struct FixedFrameCount:IComponentData
{
    public int value;
}