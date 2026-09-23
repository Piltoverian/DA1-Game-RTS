using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.LowLevel.Unsafe;

// Read baked blob references directly. No runtime allocation or initialization system.
public static class RegistryBlobLookup
{
    public static BlobAssetReference<T> GetBlobByID<T>(
        this DynamicBuffer<RegistryBlobElement> registry, FixedString64Bytes id, out FunctionResult result)
        where T : unmanaged, IGameBlobAsset
    {
        long key = RegistryTypeKey.For<T>();
        for (int i = 0; i < registry.Length; i++)
        {
            var entry = registry[i];
            if (entry.TypeKey != key) continue;
            var blob = entry.Blob.Reinterpret<T>();
            if (!blob.IsCreated || blob.Value.getID() != id) continue;
            result = FunctionResult.Success;
            return blob;
        }
        result = FunctionResult.Failure;
        return default;
    }
}

public static class RegistryTypeKey
{
    public static long For<T>() where T : unmanaged => BurstRuntime.GetHashCode64<T>();
}

public interface IGameBlobAsset
{
    FixedString64Bytes getID();
}

[InternalBufferCapacity(0)]
public struct RegistryBlobElement : IBufferElementData
{
    public long TypeKey;
    public UnsafeUntypedBlobAssetReference Blob;
}

[InternalBufferCapacity(0)]
public struct RegistryPrefabElement : IBufferElementData
{
    public Entity Prefab;
}

public struct GameDataRegistryComponent : IComponentData
{
    
}