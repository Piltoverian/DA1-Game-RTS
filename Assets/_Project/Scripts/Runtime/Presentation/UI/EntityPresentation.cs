using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public static class EntityPresentation
{
    public static Sprite GetIcon(FixedString64Bytes id)
    {
        return RegistryAuthoring.Instance != null ? RegistryAuthoring.Instance.GetIcon(id) : null;
    }

    public static string GetName(FixedString64Bytes id)
    {
        return RegistryAuthoring.Instance != null ? RegistryAuthoring.Instance.GetName(id) : id.ToString();
    }

    public static Sprite Icon(EntityManager em, Entity entity)
    {
        if (!ClientPresentationCatalog.TryGet(em, out _, out var catalog) || !em.Exists(entity)) return null;

        if (em.HasComponent<UnitComponent>(entity))
        {
            return catalog.GetIcon(em.GetComponentData<UnitComponent>(entity).DefinitionID);
        }
        if (em.HasComponent<BuildingComponent>(entity))
        {
            return catalog.GetIcon(em.GetComponentData<BuildingComponent>(entity).DefinitionID);
        }
        return null;
    }

    public static Sprite JobIcon(ProductionElement offer)
    {
        return RegistryAuthoring.Instance != null ? RegistryAuthoring.Instance.GetIcon(offer.JobID) : null;
    }

    public static Sprite JobIcon(EntityManager em, ProductionElement offer)
        => ClientPresentationCatalog.TryGet(em, out _, out var catalog) ? catalog.GetIcon(offer.JobID) : null;

    public static Sprite BuildingIcon(FixedString64Bytes buildingId)
    {
        return RegistryAuthoring.Instance != null ? RegistryAuthoring.Instance.GetIcon(buildingId) : null;
    }

    public static Sprite BuildingIcon(EntityManager em, FixedString64Bytes buildingId)
        => ClientPresentationCatalog.TryGet(em, out _, out var catalog) ? catalog.GetIcon(buildingId) : null;

    public static string BuildingName(FixedString64Bytes buildingId)
    {
        return RegistryAuthoring.Instance != null ? RegistryAuthoring.Instance.GetName(buildingId) : buildingId.ToString();
    }

    public static GameObject BuildingPreview(FixedString64Bytes buildingId)
    {
        return RegistryAuthoring.Instance != null ? RegistryAuthoring.Instance.GetBuildingPrefab(buildingId) : null;
    }

    public static GameObject BuildingPreview(EntityManager em, FixedString64Bytes buildingId)
        => ClientPresentationCatalog.TryGet(em, out _, out var catalog) ? catalog.GetBuildingPrefab(buildingId) : null;

    public static BuildingSO GetBuildingSO(FixedString64Bytes buildingId)
    {
        return RegistryAuthoring.Instance != null ? RegistryAuthoring.Instance.GetBuildingSO(buildingId) : null;
    }
}
