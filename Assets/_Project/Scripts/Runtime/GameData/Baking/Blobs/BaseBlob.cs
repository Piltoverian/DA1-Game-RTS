using Unity.Collections;
using Unity.Entities;

// Reference to another catalog record; the referenced config is not copied here.
public struct GameBlobReference
{
    public long CatalogKey;
    public FixedString64Bytes ID;
}

public struct BaseBlob : IGameBlobAsset
{
    public FixedString64Bytes ID;
    public float MaxHP;
    public float WorkRate;
    public int JobCapacity;
    // Gameplay prefab index in RegistryPrefabElement on the Registry entity.
    public int PrefabIndex;
    public BlobArray<GameBlobReference> Abilities;
    public BlobArray<GameBlobReference> Jobs;

    public FixedString64Bytes GetID() => ID;
    public FixedString64Bytes getID() => GetID();
}
