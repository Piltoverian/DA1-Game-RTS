using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.NetCode;

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

    public GameBlobReference JobReference => new GameBlobReference
    {
        CatalogKey = Kind == ProductionKind.Train ? RegistryTypeKey.For<TrainBlob>()
            : Kind == ProductionKind.Research ? RegistryTypeKey.For<ResearchBlob>() : 0,
        ID = JobID
    };
}

[GhostComponent(OwnerSendType = SendToOwnerType.SendToOwner)]
public struct ProductionQueueElement : IBufferElementData
{
    [GhostField]
    public int ItemID;
    [GhostField]
    public int PlayerID;
    [GhostField]
    public FixedString64Bytes JobID;
    [GhostField]
    public ProductionKind Kind;
    [GhostField]
    public FixedString64Bytes TechID;
    [GhostField]
    public float RemainingWork;
}
public struct ProductionPayment : IBufferElementData
{
    public int ItemID;
    public ResourceType Type;
    public float Amount;
}

[GhostComponent(OwnerSendType = SendToOwnerType.SendToOwner)]
public struct PlayerTechnology : IBufferElementData
{
    [GhostField] public FixedString64Bytes ID;
}

[GhostComponent(OwnerSendType = SendToOwnerType.SendToOwner)]
public struct PlayerPendingTech : IBufferElementData
{
    [GhostField] public FixedString64Bytes TechID;
    [GhostField] public Entity Producer;
}
