using Unity.Collections;
using Unity.Entities;

public struct PlayerContextBinding : IComponentData
{
    public int PlayerId;
    public FixedString64Bytes CivID;
    public bool Applied;
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(PlayerBootstrapSystem))]
public partial struct PlayerContextBindingSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var binding in SystemAPI.Query<RefRW<PlayerContextBinding>>())
        {
            if (binding.ValueRO.Applied) continue;
            if (!SystemAPI.TryGetSingleton<PlayerBootstrapState>(out var bootstrap) || bootstrap.Phase != PlayerBootstrapPhase.Waiting)
                continue;
            if (PlayerContextHelper.GetPlayerContextEntity(state.EntityManager, binding.ValueRO.PlayerId, out var entity, out var context) != FunctionResult.Success)
                continue;
            context.CivID = binding.ValueRO.CivID;
            state.EntityManager.SetComponentData(entity, context);
            binding.ValueRW.Applied = true;
        }
    }
}
