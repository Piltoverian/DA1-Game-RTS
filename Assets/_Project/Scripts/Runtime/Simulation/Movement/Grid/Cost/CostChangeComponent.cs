using Unity.Entities;

public struct CostChangeRequest : IBufferElementData
{
    public int newCost;
    public StartEndRect area;
}
