using System.Collections.Generic;
using Unity.Entities;

// One allocation-free selection pass serves the MonoBehaviour UI in this world.
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class ClientPresentationSelectionSystem : SystemBase
{
    readonly Dictionary<int, Entity> firstByPlayer = new();

    protected override void OnUpdate()
    {
        firstByPlayer.Clear();
        foreach (var (owner, _, entity) in SystemAPI.Query<RefRO<EntityOwner>, RefRO<Selected>>()
                     .WithAll<Selectable>().WithEntityAccess())
            firstByPlayer.TryAdd(owner.ValueRO.PlayerID, entity);
    }

    public Entity FirstForPlayer(int playerId) => firstByPlayer.TryGetValue(playerId, out var entity) ? entity : Entity.Null;
}

public static class ClientPresentationSelection
{
    public static bool TryGet(out World world, out EntityManager em, out Entity selected, out int playerId)
    {
        selected = Entity.Null;
        playerId = -1;
        if (!ClientPresentationCatalog.TryGetCurrent(out world, out em)
            || !ClientPresentationCatalog.IsReady(em) || !SelectHelper.TryGetLocalPlayerId(out playerId)) return false;
        var selection = world.GetExistingSystemManaged<ClientPresentationSelectionSystem>();
        if (selection == null) return false;
        selected = selection.FirstForPlayer(playerId);
        return selected != Entity.Null && em.Exists(selected)
            && em.HasComponent<Selected>(selected) && em.IsComponentEnabled<Selected>(selected)
            && em.HasComponent<EntityOwner>(selected) && em.GetComponentData<EntityOwner>(selected).PlayerID == playerId;
    }
}
