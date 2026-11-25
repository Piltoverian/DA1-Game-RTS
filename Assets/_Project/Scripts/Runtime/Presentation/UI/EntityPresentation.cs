using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public static class EntityPresentation
{
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

    public static Sprite JobIcon(EntityManager em, ProductionElement offer)
        => ClientPresentationCatalog.TryGet(em, out _, out var catalog) ? catalog.GetIcon(offer.JobID) : null;

    public static Sprite BuildingIcon(EntityManager em, FixedString64Bytes buildingId)
        => ClientPresentationCatalog.TryGet(em, out _, out var catalog) ? catalog.GetIcon(buildingId) : null;

    public static GameObject BuildingPreview(EntityManager em, FixedString64Bytes buildingId)
        => ClientPresentationCatalog.TryGet(em, out _, out var catalog) ? catalog.GetBuildingPrefab(buildingId) : null;

}
