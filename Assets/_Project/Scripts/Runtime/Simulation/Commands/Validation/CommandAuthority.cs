using Unity.Entities;

public static class CommandAuthority
{
    public static bool CanControl(EntityManager em, int playerId, Entity entity)
    {
        return playerId >= 0 && entity != Entity.Null && em.Exists(entity)
            && !em.HasComponent<Prefab>(entity)
            && em.HasComponent<EntityOwner>(entity)
            && em.GetComponentData<EntityOwner>(entity).PlayerID == playerId;
    }
}
