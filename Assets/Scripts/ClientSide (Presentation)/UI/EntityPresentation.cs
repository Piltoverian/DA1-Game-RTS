using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public static class EntityPresentation
{
    public static Sprite Icon(EntityManager em, Entity entity)
    {
        FixedString64Bytes id;
        bool unit = em.HasComponent<UnitComponent>(entity);
        if (unit) id = em.GetComponentData<UnitComponent>(entity).DefinitionID;
        else if (em.HasComponent<BuildingComponent>(entity)) id = em.GetComponentData<BuildingComponent>(entity).DefinitionID;
        else return null;
        foreach (var registry in Resources.LoadAll<GameDataRegistry>(""))
        {
            if (unit)
            {
                foreach (var definition in registry.Units)
                    if (definition != null && definition.basedSO != null && definition.basedSO.ID == id.ToString()) return definition.basedSO.Icon;
            }
            else
                foreach (var definition in registry.Buildings)
                    if (definition != null && definition.basedSO != null && definition.basedSO.ID == id.ToString()) return definition.basedSO.Icon;
        }
        return null;
    }
    public static Sprite JobIcon(ProductionElement offer)
    {
        foreach (var registry in Resources.LoadAll<GameDataRegistry>(""))
            foreach (var job in registry.Jobs)
                if (job != null && job.Id == offer.JobID.ToString() &&
                    ((offer.Kind == ProductionKind.Train && job is Train) || (offer.Kind == ProductionKind.Research && job is Research))) return job.Icon;
        return null;
    }

    public static Sprite BuildingIcon(FixedString64Bytes buildingId)
    {
        string idStr = buildingId.ToString();
        foreach (var registry in Resources.LoadAll<GameDataRegistry>(""))
        {
            if (registry.Buildings == null) continue;
            foreach (var definition in registry.Buildings)
            {
                if (definition != null && definition.basedSO != null && definition.basedSO.ID == idStr)
                    return definition.basedSO.Icon;
            }
        }
        return null;
    }

    public static string BuildingName(FixedString64Bytes buildingId)
    {
        string idStr = buildingId.ToString();
        foreach (var registry in Resources.LoadAll<GameDataRegistry>(""))
        {
            if (registry.Buildings == null) continue;
            foreach (var definition in registry.Buildings)
            {
                if (definition != null && definition.basedSO != null && definition.basedSO.ID == idStr)
                    return string.IsNullOrEmpty(definition.basedSO.Name) ? definition.name : definition.basedSO.Name;
            }
        }
        return idStr;
    }

    public static GameObject BuildingPreview(FixedString64Bytes buildingId)
    {
        string idStr = buildingId.ToString();
        foreach (var registry in Resources.LoadAll<GameDataRegistry>(""))
        {
            if (registry.Buildings == null) continue;
            foreach (var definition in registry.Buildings)
            {
                if (definition != null && definition.basedSO != null && definition.basedSO.ID == idStr)
                    return definition.basedSO.Prefab;
            }
        }
        return null;
    }

    public static BuildingSO GetBuildingSO(FixedString64Bytes buildingId)
    {
        string idStr = buildingId.ToString();
        foreach (var registry in Resources.LoadAll<GameDataRegistry>(""))
        {
            if (registry.Buildings == null) continue;
            foreach (var definition in registry.Buildings)
            {
                if (definition != null && definition.basedSO != null && definition.basedSO.ID == idStr)
                    return definition;
            }
        }
        return null;
    }
}
