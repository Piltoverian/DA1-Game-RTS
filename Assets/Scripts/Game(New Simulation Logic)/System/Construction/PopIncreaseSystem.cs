using Unity.Burst;
using Unity.Entities;

partial struct HouseInitSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PopIncreaseTag>();
       
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = new EntityCommandBuffer();
        foreach (var (unit,house,pendingrequest,entity) in SystemAPI.Query<Unit,HouseComponent,PopIncreaseTag>().WithEntityAccess())
        {
            PlayerContext playerContext;
            PlayerContextHelper.GetContextData(state.EntityManager, unit.playerID, out playerContext);
            playerContext.maxPopulation += house.maxPopWillIncrease;
            PlayerContextHelper.SetMaxPopulation(state.EntityManager, unit.playerID, playerContext.maxPopulation);

            ecb.RemoveComponent<PopIncreaseTag>(entity);
        }
        ecb.Playback(state.EntityManager);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
