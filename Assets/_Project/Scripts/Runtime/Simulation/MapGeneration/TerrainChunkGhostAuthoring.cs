using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

[GhostComponent(OwnerSendType=SendToOwnerType.SendToOwner)]
public struct TerrainChunkGhost : IComponentData
{
    [GhostField] public ulong Session;
    [GhostField] public uint JoinGeneration, Epoch, Revision;
    [GhostField] public int ChunkIndex, ExpectedChunks, PlayerId, Width, Height;
    [GhostField(Quantization=0)] public float CellSize;
    [GhostField(Quantization=0)] public float3 Origin, SpawnPosition;
    [GhostField] public int VisualSeed, MountainTiers, CapPixels;
    [GhostField(Quantization=0)] public float GroundRegionCells;
}

public sealed class TerrainChunkGhostAuthoring : MonoBehaviour
{
    class Baker : Baker<TerrainChunkGhostAuthoring>
    {
        public override void Bake(TerrainChunkGhostAuthoring authoring)
        {
            var e=GetEntity(TransformUsageFlags.None);
            AddComponent<CustomGhostRelevancy>(e);
            AddComponent<TerrainChunkGhost>(e);
            AddBuffer<TerrainCellGhostElement>(e);
            AddBuffer<TerrainWallVisualData>(e);
        }
    }
}
