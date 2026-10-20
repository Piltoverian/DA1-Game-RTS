using Unity.Entities;
using UnityEngine;

public class UIComponent : MonoBehaviour
{
    [SerializeField] protected int playerId = -1;
    private bool needsSnapshot = true;

    protected virtual void OnEnable()
    {
        playerId = -1;
        needsSnapshot = true;
        OnContextUnavailable();
    }

    internal bool NeedsSnapshotFor(int id) => needsSnapshot || playerId != id;

    internal void ApplyPlayerSnapshot(EntityManager manager, Entity entity, PlayerContext context)
    {
        playerId = context.PlayerId;
        needsSnapshot = false;
        OnPlayerSnapshot(manager, entity, context);
    }

    internal void ClearPlayer()
    {
        if (playerId == -1 && needsSnapshot) return;
        playerId = -1;
        needsSnapshot = true;
        OnContextUnavailable();
    }

    // Live context fields (e.g. age) have no event channel yet. Default is a no-op.
    internal void RefreshPlayerContext(PlayerContext context) => OnPlayerContextUpdated(context);

    protected virtual void OnPlayerSnapshot(EntityManager manager, Entity entity, PlayerContext context) { }
    protected virtual void OnPlayerContextUpdated(PlayerContext context) { }
    protected virtual void OnContextUnavailable() { }
}
