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

    public static void Enqueue(EntityManager em, Entity producer, GameBlobReference progression, int playerId)
    {
        if (!CommandAuthority.CanControl(em, playerId, producer)) return;
        if (!em.Exists(producer) || !em.HasComponent<ProductionData>(producer) || !em.HasComponent<EntityOwner>(producer)
            || !em.HasComponent<EntityWork>(producer) || !em.HasBuffer<ProductionElement>(producer)
            || !em.HasComponent<EntityHealth>(producer) || em.GetComponentData<EntityHealth>(producer).CurrentHP <= 0) return;
        if (em.HasComponent<BuildingConstruction>(producer) && em.GetComponentData<BuildingConstruction>(producer).Phase != ConstructionPhase.Completed) return;
        bool isTrain = progression.CatalogKey == RegistryTypeKey.For<TrainBlob>();
        if (!isTrain && progression.CatalogKey != RegistryTypeKey.For<ResearchBlob>()) return;
        var offers = em.GetBuffer<ProductionElement>(producer);
        ProductionElement offer = default;
        bool offered = false;
        foreach (var candidate in offers)
        {
            var reference = candidate.JobReference;
            if (reference.CatalogKey != progression.CatalogKey || reference.ID != progression.ID) continue;
            offer = candidate;
            offered = true;
            break;
        }
        if (!offered || !em.HasBuffer<ProductionQueueElement>(producer) || !em.HasBuffer<ProductionPayment>(producer)) return;
        var queue = em.GetBuffer<ProductionQueueElement>(producer);
        if (queue.Length >= em.GetComponentData<EntityWork>(producer).JobCapacity) return;
        int owner = em.GetComponentData<EntityOwner>(producer).PlayerID;
        if (PlayerContextHelper.GetPlayerContextEntity(em, owner, out var player, out _) != FunctionResult.Success || !TryRegistry(em, out var registryEntity)) return;
        var registry = em.GetBuffer<RegistryBlobElement>(registryEntity, true);
        var jobReference = registry.GetBlobByID(progression, out var found);
        if (found != FunctionResult.Success) return;
        float work;
        FixedString64Bytes techID = default;
        using var costs = new NativeList<ResourcePair>(Allocator.Temp);
        if (isTrain)
        {
            var job = jobReference.Reinterpret<TrainBlob>();
            var unit = registry.GetBlobByID<UnitBlob>(job.Value.OutputUnitID, out found);
            if (found != FunctionResult.Success || !em.Exists(offer.UnitPrefab) || !em.HasComponent<UnitComponent>(offer.UnitPrefab)
                || em.GetComponentData<UnitComponent>(offer.UnitPrefab).DefinitionID != job.Value.OutputUnitID) return;
            if (TechUnlockHelper.CheckUnitUnlockStatus(em, player, job.Value.OutputUnitID) != UnitUnlockStatus.Available) return;
            work = job.Value.Job.WorkLoad;
            for (int i = 0; i < unit.Value.ResourceCosts.Length; i++) costs.Add(unit.Value.ResourceCosts[i]);
        }
        else
        {
            var job = jobReference.Reinterpret<ResearchBlob>();
            if (!em.HasBuffer<PlayerTechnology>(player) || !em.HasBuffer<PlayerPendingTech>(player)) return;
            techID = job.Value.Tech.ID;
            if (TechUnlockHelper.CheckTechUnlockStatus(em, player, techID) != TechUnlockStatus.Available) return;
            work = job.Value.Job.WorkLoad;
            for (int i = 0; i < job.Value.Tech.Cost.Length; i++) costs.Add(job.Value.Tech.Cost[i]);
        }
        if (!math.isfinite(work) || work <= 0) return;
        if (!ResourcePayment.TryPay(em, owner, costs, out var paid))
        {
            paid.Dispose();
            return;
        }
        var data = em.GetComponentData<ProductionData>(producer);
        int itemID = ++data.NextItemID;
        var payments = em.GetBuffer<ProductionPayment>(producer);
        foreach (var cost in paid)
        {
            payments.Add(new ProductionPayment { ItemID = itemID, Type = cost.Type, Amount = cost.Amount });
        }
        paid.Dispose();
        queue.Add(new ProductionQueueElement { ItemID = itemID, PlayerID = owner, Offer = offer, TechID = techID, RemainingWork = work });
        if (offer.Kind == ProductionKind.Research)
            em.GetBuffer<PlayerPendingTech>(player).Add(new PlayerPendingTech { TechID = techID, Producer = producer });
        em.SetComponentData(producer, data);
    }

    public static void RemoveAt(EntityManager em, Entity producer, int index, bool refund = true)
    {
        if (!em.Exists(producer) || !em.HasBuffer<ProductionQueueElement>(producer)) return;
        var queue = em.GetBuffer<ProductionQueueElement>(producer);
        if (index < 0 || index >= queue.Length) return;
        var item = queue[index];
        bool isResearch = item.Offer.Kind == ProductionKind.Research;
        if (em.HasBuffer<ProductionPayment>(producer))
        {
            var payments = em.GetBuffer<ProductionPayment>(producer);
            for (int i = payments.Length - 1; i >= 0; i--)
            {
                if (payments[i].ItemID != item.ItemID) continue;
                if (refund) PlayerContextHelper.AddPlayerResource(em, item.PlayerID, payments[i].Type, payments[i].Amount);
                payments.RemoveAt(i);
            }
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
        queue.RemoveAt(index);
    }

    public static void RemoveFirst(EntityManager em, Entity producer, bool refund)
    {
        RemoveAt(em, producer, 0, refund);
    }

    public static void CancelAll(EntityManager em, Entity producer, bool refund = true)
    {
        if (!em.Exists(producer) || !em.HasBuffer<ProductionQueueElement>(producer)) return;
        var queue = em.GetBuffer<ProductionQueueElement>(producer);
        for (int i = queue.Length - 1; i >= 0; i--) RemoveAt(em, producer, i, refund);
    }
}

