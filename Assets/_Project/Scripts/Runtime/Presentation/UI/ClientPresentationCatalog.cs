using Unity.Entities;
using Unity.NetCode;

// This shared asset reference is baked locally, never sent through Ghosts or RPCs.
public struct PresentationCatalogReference : IComponentData
{
    public UnityObjectRef<GameDataRegistry> Catalog;
}

public static class ClientPresentationCatalog
{
    public static bool TryGetCurrent(out World world, out EntityManager em)
    {
        world = World.DefaultGameObjectInjectionWorld;
        em = default;
        if (world == null || !world.IsCreated || world.IsServer() && !world.IsClient()) return false;
        em = world.EntityManager;
        return true;
    }

    public static bool TryGet(EntityManager em, out Entity registry, out GameDataRegistry catalog)
    {
        registry = Entity.Null;
        catalog = null;
        // Do not dereference a manager retained across disposal or search another world's catalog.
        bool alive = false;
        foreach (var world in World.All)
            if (world.IsCreated && world.EntityManager == em) { alive = true; break; }
        if (!alive) return false;
        using var query = em.CreateEntityQuery(ComponentType.ReadOnly<GameDataRegistryComponent>());
        if (query.CalculateEntityCount() != 1) return false;
        var entity = query.GetSingletonEntity();
        if (!em.HasBuffer<RegistryBlobElement>(entity) || !em.HasBuffer<RegistryPrefabElement>(entity)
            || !em.HasBuffer<RegistryTechEffectElement>(entity) || !em.HasComponent<PresentationCatalogReference>(entity)) return false;
        catalog = em.GetComponentData<PresentationCatalogReference>(entity).Catalog.Value;
        if (catalog == null) return false;
        registry = entity;
        return true;
    }

    public static bool IsReady(EntityManager em) => TryGet(em, out _, out _);
}
