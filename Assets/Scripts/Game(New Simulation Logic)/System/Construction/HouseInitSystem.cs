using Unity.Burst;
using Unity.Entities;

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
partial struct HouseInitSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<HouseInitTag>();
       
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        foreach (var (unit, house, cleanup, pendingrequest, bState, entity) in SystemAPI.Query<Unit, HouseComponent, HouseCleanUp, HouseInitTag, BuildingStateComponent>().WithEntityAccess())
        {
            if (bState.Current != BuildingState.Completed)
                continue;
            PlayerContext playerContext;
            PlayerContextHelper.GetContextData(state.EntityManager, unit.playerID, out playerContext);
            playerContext.maxPopulation += house.maxPopWillIncrease;
            PlayerContextHelper.SetMaxPopulation(state.EntityManager, unit.playerID, playerContext.maxPopulation);
            HouseCleanUp cleanUp = new HouseCleanUp{ maxPopWillIncrease = house.maxPopWillIncrease,playerID = unit.playerID};
            ecb.RemoveComponent<HouseInitTag>(entity);
            ecb.SetComponent(entity, cleanUp);
        }
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
