using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public static class EntityPresentation
{
    // UI can bind its catalog explicitly; this does not change the ECS registry or commands.
    public static GameDataRegistry ResolveRegistry(GameDataRegistry preferred = null) =>
        preferred != null ? preferred : RegistryAuthoring.Instance;

    public static Sprite GetIcon(FixedString64Bytes id, GameDataRegistry preferred = null)
    {
        var registry = ResolveRegistry(preferred);
        return registry != null ? registry.GetIcon(id) : null;
    }

    public static string GetName(FixedString64Bytes id, GameDataRegistry preferred = null)
    {
        var registry = ResolveRegistry(preferred);
        return registry != null ? registry.GetName(id) : id.ToString();
    }

    public static Sprite Icon(EntityManager em, Entity entity, GameDataRegistry preferred = null)
    {
        if (em.HasComponent<UnitComponent>(entity))
        {
            return GetIcon(em.GetComponentData<UnitComponent>(entity).DefinitionID, preferred);
        }
        if (em.HasComponent<BuildingComponent>(entity))
        {
            return GetIcon(em.GetComponentData<BuildingComponent>(entity).DefinitionID, preferred);
        }
        return null;
    }

    private static Job FindJob(FixedString64Bytes id, GameDataRegistry preferred)
    {
        var registry = ResolveRegistry(preferred);
        if (registry != null && registry.Jobs != null)
            foreach (var job in registry.Jobs)
                if (job != null && job.Id == id) return job;
        return null;
    }

    public static Sprite JobIcon(ProductionElement offer, GameDataRegistry preferred = null)
    {
        var job = FindJob(offer.JobID, preferred);
        if (job != null && job.Icon != null) return job.Icon;
        if (job is Train train && train.OutputUnit != null && train.OutputUnit.basedSO != null)
            return train.OutputUnit.basedSO.Icon;
        return null;
    }

    public static string JobName(ProductionElement offer, GameDataRegistry preferred = null)
    {
        var job = FindJob(offer.JobID, preferred);
        // A short target label fits the compact tile; the producer supplies the action context.
        if (job is Train train && train.OutputUnit != null && train.OutputUnit.basedSO != null)
            return train.OutputUnit.basedSO.Name;
        return job != null && !string.IsNullOrWhiteSpace(job.DisplayName) ? job.DisplayName : offer.JobID.ToString();
    }

    public static Sprite BuildingIcon(FixedString64Bytes buildingId, GameDataRegistry preferred = null) =>
        GetIcon(buildingId, preferred);

    public static string BuildingName(FixedString64Bytes buildingId, GameDataRegistry preferred = null) =>
        GetName(buildingId, preferred);

    public static GameObject BuildingPreview(FixedString64Bytes buildingId)
    {
        return RegistryAuthoring.Instance != null ? RegistryAuthoring.Instance.GetBuildingPrefab(buildingId) : null;
    }

    public static BuildingSO GetBuildingSO(FixedString64Bytes buildingId)
    {
        return RegistryAuthoring.Instance != null ? RegistryAuthoring.Instance.GetBuildingSO(buildingId) : null;
    }
}
