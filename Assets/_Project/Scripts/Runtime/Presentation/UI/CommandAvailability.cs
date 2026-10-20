using System.Globalization;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public readonly struct CommandAvailability
{
    public readonly bool Allowed;
    public readonly bool Completed;
    public readonly string Reason;
    public readonly string Costs;
    public readonly string Detail;

    public CommandAvailability(bool allowed, string reason, string costs = "", string detail = "", bool completed = false)
    {
        Allowed = allowed; Reason = reason; Costs = costs; Detail = detail; Completed = completed;
    }
}

/// <summary>Read-only UI checks. Payment and final validation remain in simulation.</summary>
public static class CommandAvailabilityReader
{
    public static CommandAvailability Read(EntityManager em, Entity source, int playerId,
        bool isBuild, FixedString64Bytes buildingId, ProductionElement offer)
    {
        if (!CommandAuthority.CanControl(em, playerId, source))
            return new CommandAvailability(false, "Not controllable");
        if (em.HasComponent<EntityHealth>(source) && em.GetComponentData<EntityHealth>(source).CurrentHP <= 0f)
            return new CommandAvailability(false, "Object destroyed");
        if (PlayerContextHelper.GetPlayerContextEntity(em, playerId, out var player, out var context) != FunctionResult.Success ||
            !ProductionJobs.TryRegistry(em, out var registryEntity))
            return new CommandAvailability(false, "Waiting for game data");
        if (!em.HasBuffer<ResourcePair>(player))
            return new CommandAvailability(false, "Resource data unavailable");

        using var costs = new NativeList<ResourcePair>(Allocator.Temp);
        float work = 0f;
        int population = 0;
        string lockedReason = null;
        bool completed = false;
        var registry = em.GetBuffer<RegistryBlobElement>(registryEntity, true);
        if (isBuild)
        {
            if (!em.HasComponent<BuilderComponent>(source) || !em.HasComponent<MoveOverride>(source) ||
                !em.HasBuffer<BuildOffer>(source)) return new CommandAvailability(false, "Cannot build");
            bool offered = false;
            foreach (var candidate in em.GetBuffer<BuildOffer>(source, true))
                if (candidate.DefinitionID == buildingId) offered = true;
            if (!offered) return new CommandAvailability(false, "Building no longer offered");
            var definition = registry.GetBlobByID<BuildingBlob>(buildingId, out var found);
            if (found != FunctionResult.Success || !em.HasBuffer<RegistryPrefabElement>(registryEntity))
                return new CommandAvailability(false, "Building data unavailable");
            var prefabs = em.GetBuffer<RegistryPrefabElement>(registryEntity, true);
            int index = definition.Value.Base.PrefabIndex;
            if (index < 0 || index >= prefabs.Length) return new CommandAvailability(false, "Building prefab unavailable");
            var prefab = prefabs[index].Prefab;
            if (!em.Exists(prefab) || !em.HasComponent<Prefab>(prefab) || !em.HasComponent<BuildingComponent>(prefab) ||
                !em.HasComponent<EntityOwner>(prefab) || !em.HasComponent<EntityHealth>(prefab) ||
                !em.HasBuffer<EntityResourceCost>(prefab)) return new CommandAvailability(false, "Building prefab unavailable");
            work = em.GetComponentData<BuildingComponent>(prefab).WorkLoad;
            foreach (var cost in em.GetBuffer<EntityResourceCost>(prefab, true))
                costs.Add(new ResourcePair(cost.Type, cost.Amount));
        }
        else
        {
            if (!em.HasComponent<ProductionData>(source) || !em.HasComponent<EntityWork>(source) ||
                !em.HasComponent<EntityHealth>(source) || !em.HasBuffer<ProductionElement>(source) ||
                !em.HasBuffer<ProductionQueueElement>(source) || !em.HasBuffer<ProductionPayment>(source))
                return new CommandAvailability(false, "Production data unavailable");
            bool offered = false;
            foreach (var candidate in em.GetBuffer<ProductionElement>(source, true))
                if (candidate.JobReference.CatalogKey == offer.JobReference.CatalogKey && candidate.JobID == offer.JobID)
                    offered = true;
            if (!offered) return new CommandAvailability(false, "Job no longer offered");

            if (offer.Kind == ProductionKind.Train)
            {
                var job = registry.GetBlobByID<TrainBlob>(offer.JobID, out var found);
                if (found != FunctionResult.Success) return new CommandAvailability(false, "Training data unavailable");
                var unit = registry.GetBlobByID<UnitBlob>(job.Value.OutputUnitID, out found);
                if (found != FunctionResult.Success || !em.Exists(offer.UnitPrefab) || !em.HasComponent<UnitComponent>(offer.UnitPrefab) ||
                    em.GetComponentData<UnitComponent>(offer.UnitPrefab).DefinitionID != job.Value.OutputUnitID)
                    return new CommandAvailability(false, "Unit data unavailable");
                work = job.Value.Job.WorkLoad;
                population = em.GetComponentData<UnitComponent>(offer.UnitPrefab).PopulationCost;
                for (int i = 0; i < unit.Value.ResourceCosts.Length; i++) costs.Add(unit.Value.ResourceCosts[i]);
                var status = TechUnlockHelper.CheckUnitUnlockStatus(em, player, job.Value.OutputUnitID);
                if (status != UnitUnlockStatus.Available)
                    lockedReason = status == UnitUnlockStatus.Locked ? "Prerequisites not met" : "Unit unlock unavailable";
            }
            else if (offer.Kind == ProductionKind.Research)
            {
                var job = registry.GetBlobByID<ResearchBlob>(offer.JobID, out var found);
                if (found != FunctionResult.Success || !em.HasBuffer<PlayerTechnology>(player) || !em.HasBuffer<PlayerPendingTech>(player))
                    return new CommandAvailability(false, "Research data unavailable");
                work = job.Value.Job.WorkLoad;
                for (int i = 0; i < job.Value.Tech.Cost.Length; i++) costs.Add(job.Value.Tech.Cost[i]);
                var status = TechUnlockHelper.CheckTechUnlockStatus(em, player, job.Value.Tech.ID);
                completed = status == TechUnlockStatus.Completed;
                if (status != TechUnlockStatus.Available)
                    lockedReason = completed ? "Research completed" : status == TechUnlockStatus.Pending ? "Research in progress" :
                        status == TechUnlockStatus.Locked ? "Prerequisites not met" : "Research unlock unavailable";
            }
            else return new CommandAvailability(false, "Unsupported job");
        }

        if (!math.isfinite(work) || work <= 0f) return new CommandAvailability(false, "Invalid work data");
        if (!ValidCosts(costs)) return new CommandAvailability(false, "Invalid cost data");
        string costText = FormatCosts(costs);
        var detail = new StringBuilder();
        if (isBuild) detail.Append("Choose a location. Terrain and occupancy are checked during placement.");
        else
        {
            float rate = em.GetComponentData<EntityWork>(source).Rate;
            if (math.isfinite(rate) && rate > 0f)
            {
                if (math.isfinite(work / rate))
                    detail.Append("Duration: ~").Append((work / rate).ToString("0.#", CultureInfo.CurrentCulture)).Append("s");
                else detail.Append("Duration unavailable.");
            }
            else detail.Append("Production currently paused.");
            if (population > 0)
            {
                if (detail.Length > 0) detail.AppendLine();
                detail.Append("Population: ").Append(population);
                if ((long)context.currentPopulation + population > context.maxPopulation)
                    detail.AppendLine().Append("Population full: completion may wait for space.");
            }
        }

        string reason = lockedReason;
        if (!isBuild && em.HasComponent<BuildingConstruction>(source) &&
            em.GetComponentData<BuildingConstruction>(source).Phase != ConstructionPhase.Completed)
            reason = "Under construction";
        if (reason == null && !isBuild && em.GetBuffer<ProductionQueueElement>(source, true).Length >=
            em.GetComponentData<EntityWork>(source).JobCapacity) reason = "Production queue full";
        if (reason == null && !ResourcePayment.CanAfford(em, playerId, costs)) reason = "Not enough resources";
        return new CommandAvailability(reason == null, reason ?? "Available", costText, detail.ToString(),
            completed && reason == "Research completed");
    }

    private static bool ValidCosts(NativeList<ResourcePair> costs)
    {
        for (int i = 0; i < costs.Length; i++)
        {
            if (!math.isfinite(costs[i].Amount) || costs[i].Amount < 0f) return false;
            for (int j = 0; j < i; j++) if (costs[j].Type == costs[i].Type) return false;
        }
        return true;
    }

    private static string FormatCosts(NativeList<ResourcePair> costs)
    {
        var text = new StringBuilder();
        foreach (var cost in costs)
        {
            if (cost.Amount <= 0f) continue;
            if (text.Length > 0) text.Append(" / ");
            text.Append(cost.Type).Append(' ').Append(cost.Amount.ToString("0.##", CultureInfo.CurrentCulture));
        }
        return text.Length == 0 ? "Cost: Free" : "Cost: " + text;
    }
}
