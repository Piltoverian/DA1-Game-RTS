using Unity.Entities;
public static class EntityCapabilities
{
    public static bool CanBuild(EntityManager em, Entity builder, Entity site)
    {
        if (!em.Exists(builder) || !em.Exists(site) || !em.HasBuffer<BuildOffer>(builder) || !em.HasComponent<BuildingComponent>(site)
            || !em.HasComponent<EntityOwner>(builder) || !em.HasComponent<EntityOwner>(site)
            || em.GetComponentData<EntityOwner>(builder).PlayerID != em.GetComponentData<EntityOwner>(site).PlayerID) return false;
        var id = em.GetComponentData<BuildingComponent>(site).DefinitionID;
        foreach (var offer in em.GetBuffer<BuildOffer>(builder)) if (offer.DefinitionID == id) return true;
        return false;
    }
}
