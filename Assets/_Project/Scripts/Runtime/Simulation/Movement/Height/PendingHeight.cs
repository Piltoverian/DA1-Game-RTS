using Unity.Entities;

// Enabled on prefabs, inherited by newly spawned ground entities.
public struct PendingHeight : IComponentData, IEnableableComponent { }

// Absolute terrain height is added to this pivot offset, never to last frame's Y.
public struct TerrainHeightOffset : IComponentData
{
    public float Value;
}
