using Unity.Entities;
using Unity.Mathematics;
public static class FlowFieldCacheHelper
{
    public const int MAX_FLOWFIELDS = 160;

    public static Entity TryGetFieldFromCache(
        ref DynamicBuffer<FlowFieldCacheEntry> cache,
        int2 targetCell,
        uint frame)
    {
        for (int i = 0; i < cache.Length; i++)
        {
            if (math.all(cache[i].targetCell == targetCell))
            {
                FlowFieldCacheEntry entry = cache[i];

                entry.lastUsedFrame = frame;
                cache[i] = entry;

                return entry.flowField;
            }
        }

        return Entity.Null;
    }

    public static void RemoveLeastUsed(
        ref DynamicBuffer<FlowFieldCacheEntry> cache)
    {
        int oldestIndex = 0;
        uint oldestFrame = cache[0].lastUsedFrame;

        for (int i = 1; i < cache.Length; i++)
        {
            if (cache[i].lastUsedFrame < oldestFrame)
            {
                oldestFrame = cache[i].lastUsedFrame;
                oldestIndex = i;
            }
        }

        cache.RemoveAt(oldestIndex);
    }

    public static Entity CreateFlowField(EntityCommandBuffer ecb, float3 worldTarget, uint frame, GridComponent grid, EntityManager em, ref DynamicBuffer<FlowFieldCacheEntry> cache, int2 targetCell)
    {
        if (cache.Length >= MAX_FLOWFIELDS)
            RemoveLeastUsed(ref cache);
        Entity newField = FlowFieldHelper.FlowFieldInit(
            em,
            worldTarget,
            grid,
            ecb);

        cache.Add(new FlowFieldCacheEntry
        {
            targetCell = targetCell,
            flowField = newField,
            lastUsedFrame = frame
        });
        return newField;
    }
}