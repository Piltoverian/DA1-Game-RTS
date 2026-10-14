using Unity.Collections;

/// <summary>
/// Dial queue for non-negative integer edge weights up to 268 (cell 254 + diagonal 14).
/// Each entry remembers its insertion cost; callers discard stale entries.
/// Storage slots are recycled on pop, so memory follows peak queue size, not total pushes.
/// </summary>
public struct IntegrationBucketQueue
{
    public const int MaxEdgeCost = 268;
    public const int BucketCount = MaxEdgeCost + 1;

    private struct Entry
    {
        public int Node;
        public int Cost;
        public int Next;
    }

    private NativeArray<int> heads;
    private NativeArray<int> tails;
    private NativeList<Entry> entries;
    private int freeHead;
    private int currentCost;
    private int count;

    public IntegrationBucketQueue(Allocator allocator)
    {
        heads = new NativeArray<int>(BucketCount, allocator, NativeArrayOptions.UninitializedMemory);
        tails = new NativeArray<int>(BucketCount, allocator, NativeArrayOptions.UninitializedMemory);
        entries = new NativeList<Entry>(256, allocator);
        for (int i = 0; i < BucketCount; i++)
        {
            heads[i] = -1;
            tails[i] = -1;
        }
        freeHead = -1;
        currentCost = 0;
        count = 0;
    }

    public void Enqueue(int node, int cost)
    {
        int slot;
        if (freeHead >= 0)
        {
            slot = freeHead;
            freeHead = entries[slot].Next;
        }
        else
        {
            slot = entries.Length;
            entries.Add(default);
        }
        entries[slot] = new Entry { Node = node, Cost = cost, Next = -1 };
        int bucket = cost % BucketCount;
        int tail = tails[bucket];
        if (tail >= 0)
        {
            Entry previous = entries[tail];
            previous.Next = slot;
            entries[tail] = previous;
        }
        else
        {
            heads[bucket] = slot;
        }
        tails[bucket] = slot;
        count++;
    }

    public bool TryDequeue(out int node, out int cost)
    {
        if (count == 0)
        {
            node = 0;
            cost = 0;
            return false;
        }
        int bucket = currentCost % BucketCount;
        while (heads[bucket] < 0)
        {
            currentCost++;
            bucket = currentCost % BucketCount;
        }
        int slot = heads[bucket];
        Entry entry = entries[slot];
        heads[bucket] = entry.Next;
        if (entry.Next < 0)
            tails[bucket] = -1;
        node = entry.Node;
        cost = entry.Cost;
        entry.Next = freeHead;
        entries[slot] = entry;
        freeHead = slot;
        count--;
        return true;
    }

    public void Dispose()
    {
        heads.Dispose();
        tails.Dispose();
        entries.Dispose();
    }
}
