using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(WorkerGatherSystem))]
[BurstCompile]
public partial struct ResourceNodeDepletedSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (node, entity) in
                 SystemAPI.Query<RefRO<ResourceNodeData>>()
                     .WithAll<ResourceNodeTag>()
                     .WithEntityAccess())
        {
            if (node.ValueRO.Amount <= 0)
            {
                ecb.DestroyEntity(entity);
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
