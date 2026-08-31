using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

partial struct HouseCleanUpSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (cleanup, entity) in SystemAPI.Query<HouseCleanUp>().WithNone<HouseComponent>().WithEntityAccess())
        {
            PlayerContext playerContext;
            PlayerContextHelper.GetContextData(state.EntityManager, cleanup.playerID, out playerContext);
            playerContext.maxPopulation -= cleanup.maxPopWillIncrease;
            PlayerContextHelper.SetMaxPopulation(state.EntityManager, cleanup.playerID, playerContext.maxPopulation);
            ecb.RemoveComponent<HouseCleanUp>(entity);
        }
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
