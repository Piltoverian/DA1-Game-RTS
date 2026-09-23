using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

// Scene bindings only. All offers and gameplay settings come from BasedSO.Jobs.
public class ProductionAuthoring : MonoBehaviour
{
    public Transform SpawnOffset;
    public Transform RallyOffset;
}
public struct ProductionData : IComponentData
{
    public float3 SpawnOffset;
    public float3 RallyOffset;
    public int NextItemID;
}
public enum ProductionKind : byte { Train, Research }
public struct ProductionElement : IBufferElementData
{
    public FixedString64Bytes JobID;
    public ProductionKind Kind;
    public Entity UnitPrefab;
}
public struct ProductionQueueElement : IBufferElementData
{
    public int ItemID;
    public int PlayerID;
    public ProductionElement Offer;
    public FixedString64Bytes TechID;
    public float RemainingWork;
}
public struct ProductionPayment : IBufferElementData
{
    public int ItemID;
    public ResourceType Type;
    public float Amount;
}
public struct PlayerTechnology : IBufferElementData
{
    public FixedString64Bytes ID;
}

public struct PlayerPendingTech : IBufferElementData
{
    public FixedString64Bytes TechID;
    public Entity Producer;
}
