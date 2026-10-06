using Unity.Entities;
public static class TrainUnitHelper
{
    public static void TrainUnit(EntityManager entityManager, Entity buildingEntity, int indexInPrefabList)
        => ProductionJobs.Enqueue(entityManager, buildingEntity, indexInPrefabList);
}
public static class BuildingHelper
{
    public static bool IsUnderConstruction(EntityManager entityManager, Entity buildingEntity)
    {
        if (!entityManager.Exists(buildingEntity))
            return false;
        if (!entityManager.HasComponent<BuildingConstruction>(buildingEntity))
            return false;
        var state = entityManager.GetComponentData<BuildingConstruction>(buildingEntity);
        return state.Phase != ConstructionPhase.Completed;
    }

    public static bool CanBuildOrRepair(EntityManager entityManager, Entity buildingEntity)
    {
        if (!entityManager.Exists(buildingEntity))
            return false;
        if (!entityManager.HasComponent<BuildingConstruction>(buildingEntity))
            return false;
        var state = entityManager.GetComponentData<BuildingConstruction>(buildingEntity);
        if (state.Phase == ConstructionPhase.Planned || state.Phase == ConstructionPhase.UnderConstruction)
            return true;
        if (state.Phase == ConstructionPhase.Completed && entityManager.HasComponent<EntityHealth>(buildingEntity))
        {
            var h = entityManager.GetComponentData<EntityHealth>(buildingEntity);
            return h.CurrentHP < h.MaxHP;
        }
        return false;
    }
}
