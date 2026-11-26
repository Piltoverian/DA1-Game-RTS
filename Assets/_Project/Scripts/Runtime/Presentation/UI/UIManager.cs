using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Unity.NetCode;

public class UIManager : MonoBehaviour
{
    [SerializeField] private int playerId = -1;
    private UIComponent[] components;
    private World world;
    private EntityQuery localPlayerQuery;
    private EntityQuery bootstrapQuery;
    private EntityQuery contextsQuery;
    private Entity contextEntity;
    private TestPlayer fallbackPlayer;

    // Kept for existing Inspector/API references; runtime identity comes from the local context.
    public int PlayerId { get => playerId; set { playerId = value; contextEntity = Entity.Null; } }

    public static bool TryGetPlayer(out EntityManager em, out int id)
    {
        em = default;
        id = -1;
        World client = null, local = null;
        foreach (var world in World.All)
        {
            if (!world.IsCreated) continue;
            if (world.IsClient())
            {
                // A single UI cannot choose between multiple client sessions.
                if (client != null) return false;
                client = world;
            }
            else if (!world.IsServer() && (world.Flags & WorldFlags.Game) == WorldFlags.Game)
            {
                if (local != null) return false;
                local = world;
            }
        }
        if (client != null)
        {
            if (!PlayerNetwork.TryLocal(client, out _, out var player)) return false;
            em = client.EntityManager;
            id = player.PlayerId;
            return true;
        }
        var manager = FindAnyObjectByType<UIManager>();
        if (local == null || manager == null) return false;
        em = local.EntityManager;
        id = SelectHelper.TryGetLocalPlayerId(out int localId) ? localId : -1;
        return id >= 0;
    }

    private void Awake() => components = GetComponentsInChildren<UIComponent>(true);

    private void LateUpdate()
    {
        var currentWorld = TryGetPlayer(out var currentManager, out _) ? currentManager.World : null;
        if (currentWorld != world)
        {
            ReleaseQueries();
            ClearPlayer();
            if (currentWorld != null && currentWorld.IsCreated)
            {
                world = currentWorld;
                localPlayerQuery = world.EntityManager.CreateEntityQuery(typeof(LocalTestPlayer));
                bootstrapQuery = world.EntityManager.CreateEntityQuery(typeof(PlayerBootstrapState));
                contextsQuery = world.EntityManager.CreateEntityQuery(typeof(PlayerContext));
            }
        }

        if (world == null || !world.IsCreated || !TryResolvePlayer(out int id))
        {
            ClearPlayer();
            return;
        }

        var manager = world.EntityManager;
        if (id != playerId || !manager.Exists(contextEntity) ||
            !manager.HasComponent<PlayerContext>(contextEntity) ||
            manager.GetComponentData<PlayerContext>(contextEntity).PlayerId != id)
        {
            // A replacement context with the same player ID also needs a fresh snapshot.
            foreach (var component in components)
                if (component != null) component.ClearPlayer();
            contextEntity = Entity.Null;
            using var entities = contextsQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                if (manager.GetComponentData<PlayerContext>(entity).PlayerId != id) continue;
                contextEntity = entity;
                break;
            }
        }

        if (contextEntity == Entity.Null)
        {
            ClearPlayer();
            return;
        }

        playerId = id;
        var context = manager.GetComponentData<PlayerContext>(contextEntity);
        foreach (var component in components)
        {
            if (component == null || !component.isActiveAndEnabled) continue;
            if (component.NeedsSnapshotFor(id)) component.ApplyPlayerSnapshot(manager, contextEntity, context);
            component.RefreshPlayerContext(context);
        }
    }

    private bool TryResolvePlayer(out int id)
    {
        if (world != null && world.IsCreated && world.IsClient())
        {
            if (PlayerNetwork.TryLocal(world, out _, out var identity)) { id = identity.PlayerId; return true; }
            id = -1; return false;
        }
        id = -1;
        int bootstrapCount = bootstrapQuery.CalculateEntityCount();
        if (bootstrapCount > 1 || (bootstrapCount == 1 &&
            bootstrapQuery.GetSingleton<PlayerBootstrapState>().Phase != PlayerBootstrapPhase.Ready)) return false;

        int count = localPlayerQuery.CalculateEntityCount();
        if (count > 1) return false;
        if (count == 1)
        {
            id = localPlayerQuery.GetSingleton<LocalTestPlayer>().PlayerId;
            return id >= 0;
        }

        // Same authoring fallback as SelectManager for scenes without a baked local identity.
        if (fallbackPlayer == null) fallbackPlayer = FindAnyObjectByType<TestPlayer>();
        if (fallbackPlayer == null || !fallbackPlayer.isActiveAndEnabled) return false;
        id = fallbackPlayer.playerId;
        return id >= 0;
    }

    private void ClearPlayer()
    {
        playerId = -1;
        contextEntity = Entity.Null;
        if (components == null) return;
        foreach (var component in components)
            if (component != null) component.ClearPlayer();
    }

    private void ReleaseQueries()
    {
        if (world != null && world.IsCreated)
        {
            localPlayerQuery.Dispose();
            bootstrapQuery.Dispose();
            contextsQuery.Dispose();
        }
        world = null;
    }

    private void OnDisable()
    {
        ClearPlayer();
        ReleaseQueries();
    }
}
