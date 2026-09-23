using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public static class ProductionJobs
{
    public static bool TryRegistry(EntityManager em, out Entity entity)
    {
        using var query = em.CreateEntityQuery(ComponentType.ReadOnly<GameDataRegistryComponent>(), ComponentType.ReadOnly<RegistryBlobElement>());
        entity = query.CalculateEntityCount() == 1 ? query.GetSingletonEntity() : Entity.Null;
        return entity != Entity.Null;
    }

    public static void Enqueue(EntityManager em, Entity producer, int index)
    {
        if (!em.Exists(producer) || !em.HasComponent<ProductionData>(producer) || !em.HasComponent<EntityOwner>(producer)
            || !em.HasComponent<EntityWork>(producer) || !em.HasBuffer<ProductionElement>(producer)
            || !em.HasComponent<EntityHealth>(producer) || em.GetComponentData<EntityHealth>(producer).CurrentHP <= 0) return;
        if (em.HasComponent<BuildingConstruction>(producer) && em.GetComponentData<BuildingConstruction>(producer).Phase != ConstructionPhase.Completed) return;
        var offers = em.GetBuffer<ProductionElement>(producer);
        var queue = em.GetBuffer<ProductionQueueElement>(producer);
        if (index < 0 || index >= offers.Length || queue.Length >= em.GetComponentData<EntityWork>(producer).JobCapacity) return;
        int owner = em.GetComponentData<EntityOwner>(producer).PlayerID;
        if (PlayerContextHelper.GetPlayerContextEntity(em, owner, out var player, out _) != FunctionResult.Success || !TryRegistry(em, out var registryEntity)) return;
        var registry = em.GetBuffer<RegistryBlobElement>(registryEntity, true);
        var offer = offers[index];
        float work;
        FixedString64Bytes techID = default;
        using var costs = new NativeList<ResourcePair>(Allocator.Temp);
        if (offer.Kind == ProductionKind.Train)
        {
            var job = registry.GetBlobByID<TrainBlob>(offer.JobID, out var found);
            if (found != FunctionResult.Success) return;
            var unit = registry.GetBlobByID<UnitBlob>(job.Value.OutputUnitID, out found);
            if (found != FunctionResult.Success || !em.Exists(offer.UnitPrefab) || !em.HasComponent<UnitComponent>(offer.UnitPrefab)
                || em.GetComponentData<UnitComponent>(offer.UnitPrefab).DefinitionID != job.Value.OutputUnitID) return;
            if (TechUnlockHelper.CheckUnitUnlockStatus(em, owner, job.Value.OutputUnitID) != UnitUnlockStatus.Available) return;
            work = job.Value.Job.WorkLoad;
            for (int i = 0; i < unit.Value.ResourceCosts.Length; i++) costs.Add(unit.Value.ResourceCosts[i]);
        }
        else
        {
            var job = registry.GetBlobByID<ResearchBlob>(offer.JobID, out var found);
            if (found != FunctionResult.Success || !em.HasBuffer<PlayerTechnology>(player) || !em.HasBuffer<PlayerPendingTech>(player)) return;
            techID = job.Value.Tech.ID;
            if (TechUnlockHelper.CheckTechUnlockStatus(em, owner, techID) != TechUnlockStatus.Available) return;
            work = job.Value.Job.WorkLoad;
            for (int i = 0; i < job.Value.Tech.Cost.Length; i++) costs.Add(job.Value.Tech.Cost[i]);
        }
        if (!math.isfinite(work) || work <= 0) return;
        for (int i = 0; i < costs.Length; i++)
        {
            var cost = costs[i];
            if (!math.isfinite(cost.Amount) || cost.Amount < 0) return;
            for (int j = costs.Length - 1; j > i; j--)
                if (costs[j].Type == cost.Type) { cost.Amount += costs[j].Amount; costs.RemoveAt(j); }
            costs.ElementAt(i) = cost;
            if (PlayerContextHelper.GetPlayerResourceByType(em, owner, cost.Type, out float available) != FunctionResult.Success || available < cost.Amount) return;
        }
        var data = em.GetComponentData<ProductionData>(producer);
        int itemID = ++data.NextItemID;
        var payments = em.GetBuffer<ProductionPayment>(producer);
        foreach (var cost in costs)
        {
            PlayerContextHelper.AddPlayerResource(em, owner, cost.Type, -cost.Amount);
            payments.Add(new ProductionPayment { ItemID = itemID, Type = cost.Type, Amount = cost.Amount });
        }
        queue.Add(new ProductionQueueElement { ItemID = itemID, PlayerID = owner, Offer = offer, TechID = techID, RemainingWork = work });
        if (offer.Kind == ProductionKind.Research)
            em.GetBuffer<PlayerPendingTech>(player).Add(new PlayerPendingTech { TechID = techID, Producer = producer });
        em.SetComponentData(producer, data);
    }

    public static void RemoveFirst(EntityManager em, Entity producer, bool refund)
    {
        var queue = em.GetBuffer<ProductionQueueElement>(producer);
        if (queue.IsEmpty) return;
        var item = queue[0];
        bool isResearch = item.Offer.Kind == ProductionKind.Research;
        bool shouldRefund = refund || isResearch;
        var payments = em.GetBuffer<ProductionPayment>(producer);
        for (int i = payments.Length - 1; i >= 0; i--)
        {
            if (payments[i].ItemID != item.ItemID) continue;
            if (shouldRefund) PlayerContextHelper.AddPlayerResource(em, item.PlayerID, payments[i].Type, payments[i].Amount);
            payments.RemoveAt(i);
        }
        if (isResearch && PlayerContextHelper.GetPlayerContextEntity(em, item.PlayerID, out var player, out _) == FunctionResult.Success)
        {
            if (em.HasBuffer<PlayerPendingTech>(player))
            {
                var pending = em.GetBuffer<PlayerPendingTech>(player);
                for (int i = pending.Length - 1; i >= 0; i--)
                {
                    if (pending[i].TechID == item.TechID && pending[i].Producer == producer)
                    {
                        pending.RemoveAt(i);
                        break;
                    }
                }
            }
        }
        queue.RemoveAt(0);
    }

    public static void CancelAll(EntityManager em, Entity producer, bool refund = true)
    {
        if (!em.HasBuffer<ProductionQueueElement>(producer)) return;
        while (em.GetBuffer<ProductionQueueElement>(producer).Length > 0) RemoveFirst(em, producer, refund);
    }
}

