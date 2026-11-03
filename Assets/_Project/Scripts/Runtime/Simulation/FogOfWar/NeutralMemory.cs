using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

// The ghost spawn key survives relevance despawns and never exposes a server Entity index.
public static class NeutralMemoryIdentity
{
    public static ulong FromGhost(in GhostInstance ghost) => ghost.ghostId == 0 ? 0
        : ((ulong)ghost.spawnTick.SerializedData << 32) | (uint)ghost.ghostId;

    public static bool IsNeutralGameplay(EntityManager em, Entity entity) =>
        em.Exists(entity) && !em.HasComponent<Prefab>(entity) && !em.HasComponent<GhostChildEntity>(entity)
        && !em.HasComponent<Bullet>(entity) && !em.HasComponent<ArtilleryBullet>(entity)
        && !em.HasComponent<ResourceNodePendingConfig>(entity)
        && (!em.HasComponent<EntityOwner>(entity) || em.GetComponentData<EntityOwner>(entity).PlayerID < 0)
        && (em.HasComponent<UnitComponent>(entity) || em.HasComponent<BuildingComponent>(entity)
            || em.HasComponent<ResourceNodeData>(entity) || em.HasComponent<BlockageData>(entity));
}

public struct NeutralMemoryRecord
{
    public ulong Id, Revision;
    public NetworkTick ObservedTick;
    public bool Removed;
    public int TemplateGhostType;
    public ResourceType ResourceType;
    public FogEntityRenderData Data;
}

// One persistent store per player, independent of that player's current connection.
// Only complete authorized observations enter here; absence of a client ghost is never evidence.
public sealed class NeutralMemoryStore
{
    readonly Dictionary<ulong, NeutralMemoryRecord> records = new();
    readonly HashSet<ulong> observed = new();
    readonly HashSet<int> visible = new();
    readonly List<ulong> stale = new();
    public IReadOnlyDictionary<ulong, NeutralMemoryRecord> Records => records;
    public ulong Revision { get; private set; }

    public void Observe(FogOfWarFrame frame, IReadOnlyList<NeutralMemoryRecord> observations, NetworkTick tick = default)
    {
        if (!frame.IsValid) return;
        observed.Clear(); visible.Clear(); stale.Clear();
        foreach (int cell in frame.VisibleCells) visible.Add(cell);
        foreach (var incoming in observations)
        {
            if (incoming.Id == 0 || incoming.Data.Kind == FogRenderKind.Projectile) continue;
            observed.Add(incoming.Id);
            if (incoming.Data.OwnerId >= 0) { Remove(incoming.Id, tick); continue; }
            var value = incoming;
            // A record contains frozen public presentation values, never simulation references.
            value.Data.Entity = Entity.Null; value.Data.SourceEntity = null;
            value.Data.ActiveJob = null; // Production queues retain their owner-only replication policy.
            value.Data.NeutralId = value.Id; value.Data.IsVisible = false;
            value.Removed = false;
            value.ObservedTick = tick;
            if (records.TryGetValue(value.Id, out var old) && !old.Removed
                && old.TemplateGhostType == value.TemplateGhostType && old.ResourceType == value.ResourceType
                && old.Data.Equals(value.Data)) continue;
            value.Revision = ++Revision;
            records[value.Id] = value;
        }
        foreach (var pair in records)
        {
            if (pair.Value.Removed || observed.Contains(pair.Key)) continue;
            var data = pair.Value.Data;
            bool emptyObserved = false;
            for (int y = data.FootprintMin.y; y <= data.FootprintMax.y && !emptyObserved; y++)
            for (int x = data.FootprintMin.x; x <= data.FootprintMax.x; x++)
                if (visible.Contains(y * frame.Width + x)) { emptyObserved = true; break; }
            if (emptyObserved) stale.Add(pair.Key);
        }
        foreach (ulong id in stale) Remove(id, tick);
    }

    void Remove(ulong id, NetworkTick tick)
    {
        if (!records.TryGetValue(id, out var record) || record.Removed) return;
        record.Removed = true; record.ObservedTick = tick; record.Revision = ++Revision; records[id] = record;
    }

    public bool TryKnownTarget(ulong id, in GridComponent grid, out FogKnownTarget known)
    {
        known = default;
        if (!records.TryGetValue(id, out var value) || value.Removed || value.Data.OwnerId >= 0
            || value.Data.Kind != FogRenderKind.Resource || !value.Data.Resource.HasValue
            || value.Data.Resource.Value.Amount <= 0 || !SpatialCellCoverage.IsGridValid(grid)) return false;
        known = new FogKnownTarget { HasValue = true, Kind = value.Data.Kind,
            Position = value.Data.Transform.Position, ResourceType = value.ResourceType,
            Min = grid.origin.xz + (float2)value.Data.FootprintMin * grid.cellsize,
            Max = grid.origin.xz + ((float2)value.Data.FootprintMax + 1) * grid.cellsize };
        return true;
    }
}

// Frozen snapshot pages and live deltas share entry revisions, so their arrival order is immaterial.
public sealed class NeutralMemoryReplica
{
    readonly Dictionary<ulong, NeutralMemoryRecord> records = new();
    readonly HashSet<int> received = new();
    public IReadOnlyDictionary<ulong, NeutralMemoryRecord> Records => records;
    public NeutralMemoryHeaderRpc Header { get; private set; }
    bool ended;
    public bool Ready => Header.StreamGeneration != 0 && ended && received.Count == Header.Count;

    public void Clear() { records.Clear(); received.Clear(); Header = default; ended = false; }

    public void Begin(in NeutralMemoryHeaderRpc header)
    {
        if (header.StreamGeneration == 0 || header.Count < 0 || header.Count > 1000000) return;
        bool sameScope = Header.MatchSessionId == header.MatchSessionId
            && Header.JoinGeneration == header.JoinGeneration && Header.Epoch == header.Epoch;
        if (sameScope && header.StreamGeneration <= Header.StreamGeneration) return;
        Clear(); Header = header;
    }

    public void Apply(in NeutralMemoryEntryRpc entry)
    {
        if (!Matches(entry.MatchSessionId, entry.JoinGeneration, entry.Epoch, entry.StreamGeneration)
            || entry.NeutralId == 0 || entry.Revision == 0) return;
        if (entry.SnapshotIndex >= 0)
        {
            if (entry.SnapshotIndex >= Header.Count || entry.Revision > Header.CutoffRevision) return;
            received.Add(entry.SnapshotIndex);
        }
        if (records.TryGetValue(entry.NeutralId, out var newer) && newer.Revision >= entry.Revision) return;
        var incoming = entry.ToRecord();
        if (newer.ObservedTick.IsValid && (!incoming.ObservedTick.IsValid || newer.ObservedTick.IsNewerThan(incoming.ObservedTick)))
        {
            // Ghost snapshots and RPC pages use different channels. Preserve the newer live observation.
            newer.Revision = incoming.Revision; records[entry.NeutralId] = newer;
        }
        else records[entry.NeutralId] = incoming;
    }

    public void ObserveLive(in NeutralMemoryRecord live)
    {
        if (live.Id == 0 || !live.ObservedTick.IsValid || live.Data.Kind == FogRenderKind.Projectile) return;
        bool exists = records.TryGetValue(live.Id, out var old);
        if (exists && old.ObservedTick.IsValid && !live.ObservedTick.IsNewerThan(old.ObservedTick)) return;
        if (live.Data.OwnerId >= 0)
        {
            if (exists) { old.Removed = true; old.ObservedTick = live.ObservedTick; records[live.Id] = old; }
            return;
        }
        var value = live;
        value.Revision = exists ? old.Revision : 0;
        value.Data.Entity = Entity.Null; value.Data.SourceEntity = null;
        value.Data.ActiveJob = null;
        value.Data.NeutralId = value.Id; value.Data.IsVisible = false;
        records[value.Id] = value;
    }

    public void End(in NeutralMemoryDoneRpc done)
    {
        if (Matches(done.MatchSessionId, done.JoinGeneration, done.Epoch, done.StreamGeneration)
            && done.CutoffRevision == Header.CutoffRevision && done.Count == Header.Count) ended = true;
    }

    bool Matches(ulong session, uint join, uint epoch, uint stream) => stream != 0
        && Header.MatchSessionId == session && Header.JoinGeneration == join
        && Header.Epoch == epoch && Header.StreamGeneration == stream;
}
