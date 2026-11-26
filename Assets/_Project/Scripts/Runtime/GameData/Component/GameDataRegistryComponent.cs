using System;
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
        return registry.GetBlobByID(RegistryTypeKey.For<T>(), id, out result).Reinterpret<T>();
    }

    public static UnsafeUntypedBlobAssetReference GetBlobByID(
        this DynamicBuffer<RegistryBlobElement> registry, GameBlobReference reference, out FunctionResult result)
        => registry.GetBlobByID(reference.CatalogKey, reference.ID, out result);

    public static UnsafeUntypedBlobAssetReference GetBlobByID(
        this DynamicBuffer<RegistryBlobElement> registry, long typeKey, FixedString64Bytes id, out FunctionResult result)
    {
        // RegistryAuthoring sorts once during baking. Find the first matching pair.
        var target = new RegistryBlobElement { TypeKey = typeKey, ID = id };
        int low = 0, high = registry.Length;
        while (low < high)
        {
            int middle = low + (high - low) / 2;
            if (registry[middle].CompareTo(target) < 0) low = middle + 1;
            else high = middle;
        }
        // Preserve the first valid reference if duplicate catalog records exist.
        for (int i = low; i < registry.Length && registry[i].CompareTo(target) == 0; i++)
        {
            var entry = registry[i];
            if (!entry.Blob.Reinterpret<byte>().IsCreated) continue;
            result = FunctionResult.Success;
            return entry.Blob;
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
public struct RegistryBlobElement : IBufferElementData, IComparable<RegistryBlobElement>
{
    public long TypeKey;
    public FixedString64Bytes ID;
    public UnsafeUntypedBlobAssetReference Blob;

    public int CompareTo(RegistryBlobElement other)
    {
        int typeOrder = TypeKey.CompareTo(other.TypeKey);
        return typeOrder != 0 ? typeOrder : ID.CompareTo(other.ID);
    }

    public static RegistryBlobElement Create<T>(BlobAssetReference<T> blob) where T : unmanaged, IGameBlobAsset
        => new RegistryBlobElement
        {
            TypeKey = RegistryTypeKey.For<T>(),
            ID = blob.IsCreated ? blob.Value.getID() : default,
            Blob = UnsafeUntypedBlobAssetReference.Create(blob)
        };
}

[InternalBufferCapacity(0)]
public struct RegistryPrefabElement : IBufferElementData
{
    public Entity Prefab;
}

[InternalBufferCapacity(0)]
public struct RegistryTechEffectElement : IBufferElementData
{
    public UnityObjectRef<TechEffect> Effect;
}

public struct GameDataRegistryComponent : IComponentData
{
    
}
