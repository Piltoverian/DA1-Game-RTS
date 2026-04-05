using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

partial struct SelectedVisualSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (RefRO<Selected> selected in SystemAPI.Query<RefRO<Selected>>())
        {
            RefRW<LocalTransform> visualLocalTransform =
                SystemAPI.GetComponentRW<LocalTransform>(selected.ValueRO.visualEntity);

            visualLocalTransform.ValueRW =
                visualLocalTransform.ValueRO.WithScale(selected.ValueRO.showScale);
        }
    }
}