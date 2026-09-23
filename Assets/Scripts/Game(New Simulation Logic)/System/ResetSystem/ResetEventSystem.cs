using Unity.Burst;
using Unity.Entities;

[UpdateInGroup(typeof(LateSimulationSystemGroup), OrderLast = true)]
partial struct ResetEventSystem : ISystem
{

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (RefRW<EntityHealth> health in SystemAPI.Query<RefRW<EntityHealth>>())
        {
            health.ValueRW.Changed = false;
        }
    }

}
