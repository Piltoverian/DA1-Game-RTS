using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

public struct DOTSSelectManagerComponent : IComponentData
{
    
}

partial struct DOTSSelectManagerSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        Entity et= state.EntityManager.CreateSingleton<DOTSSelectManagerComponent>();
        state.EntityManager.AddBuffer<SelectionRequest>(et);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {

    }
}
