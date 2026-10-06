using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(ProductionSystem))]
public partial struct PopulationSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;
        // Instantiate strips cleanup components. Register preplaced and externally spawned entities too.
        var registration = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (owner, entity) in SystemAPI.Query<RefRO<EntityOwner>>().WithAll<EntityHealth>().WithNone<PopulationAccount>().WithEntityAccess())
            if (em.HasComponent<UnitComponent>(entity) || em.HasComponent<BuildingComponent>(entity))
                registration.AddComponent(entity, new PopulationAccount { PlayerID = -1 });
        registration.Playback(em);
        registration.Dispose();
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (accountRef, entity) in SystemAPI.Query<RefRW<PopulationAccount>>().WithEntityAccess())
        {
            var old = accountRef.ValueRO;
            bool alive = em.HasComponent<EntityOwner>(entity) && em.HasComponent<EntityHealth>(entity)
                && em.GetComponentData<EntityHealth>(entity).CurrentHP > 0f;
            int owner = alive ? em.GetComponentData<EntityOwner>(entity).PlayerID : -1;
            if (em.HasComponent<Selectable>(entity) && em.HasComponent<EntityOwner>(entity))
            {
                var selection = em.GetComponentData<Selectable>(entity);
                selection.playerID = em.GetComponentData<EntityOwner>(entity).PlayerID;
                em.SetComponentData(entity, selection);
            }
            int used = alive && em.HasComponent<UnitComponent>(entity) ? em.GetComponentData<UnitComponent>(entity).PopulationCost : 0;
            int capacity = alive && em.HasComponent<BuildingComponent>(entity) && em.HasComponent<BuildingConstruction>(entity)
                && em.GetComponentData<BuildingConstruction>(entity).Phase == ConstructionPhase.Completed
                ? em.GetComponentData<BuildingComponent>(entity).PopulationCapacity : 0;
            if (owner < 0 || PlayerContextHelper.GetContextData(em, owner, out _) != FunctionResult.Success)
            { owner = -1; used = 0; capacity = 0; }
            if (old.PlayerID != owner || old.Used != used || old.Capacity != capacity)
            {
                Adjust(em, old.PlayerID, -old.Used, -old.Capacity);
                Adjust(em, owner, used, capacity);
                accountRef.ValueRW = new PopulationAccount { PlayerID = owner, Used = used, Capacity = capacity };
            }
            if (!em.HasComponent<EntityOwner>(entity)) ecb.RemoveComponent<PopulationAccount>(entity);
        }
        ecb.Playback(em);
        ecb.Dispose();
    }

    public static void Adjust(EntityManager em, int player, int used, int capacity)
    {
        if (player < 0 || PlayerContextHelper.GetContextData(em, player, out var context) != FunctionResult.Success) return;
        PlayerContextHelper.SetCurrentPopulation(em, player, math.max(0, context.currentPopulation + used));
        PlayerContextHelper.SetMaxPopulation(em, player, math.max(0, context.maxPopulation + capacity));
    }
}

