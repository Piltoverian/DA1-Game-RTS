using Unity.Collections;
using Unity.Entities;

public struct MovementAgentBucket : IComponentData
{
    public NativeParallelMultiHashMap<int, Entity> Bucket;
}
