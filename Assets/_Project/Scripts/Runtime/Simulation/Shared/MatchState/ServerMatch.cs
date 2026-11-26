using Unity.Burst;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Main-thread phase API for one active authoritative match. Client state and
/// replication are separate. Local simulation is supported until a server starts.
/// All phase writes must go through SetPhase; direct Inspector edits bypass cache.
/// </summary>
public static class ServerMatch
{
    struct CachedState
    {
        public ulong Owner;
        public MatchPhase Phase;
        public bool Initialized;
        public bool IsServer;
    }

    static readonly SharedStatic<CachedState> Cache = SharedStatic<CachedState>.GetOrCreate<ServerMatchCacheKey>();
    struct ServerMatchCacheKey { }

    public static bool CanRun => Cache.Data.Initialized && Cache.Data.Phase == MatchPhase.Running;
    public static MatchPhase Phase => Cache.Data.Initialized ? Cache.Data.Phase : MatchPhase.Preparing;

    /// <summary>Local gameplay bypasses the match gate; server gameplay uses its phase cache.</summary>
    public static bool isGamePlayCanRun(ref SystemState state)
    {
        var flags = state.WorldUnmanaged.Flags;
        if ((flags & WorldFlags.GameServer) == WorldFlags.GameServer)
            return Cache.Data.IsServer
                && Cache.Data.Owner == state.WorldUnmanaged.SequenceNumber
                && CanRun;
        if ((flags & WorldFlags.GameClient) == WorldFlags.GameClient
            || (flags & WorldFlags.GameThinClient) == WorldFlags.GameThinClient)
            return false;

        // Game includes the shared Live bit, so testing != 0 would also accept
        // Editor/server/client worlds. Require all bits of Game for local play.
        return (flags & WorldFlags.Game) == WorldFlags.Game;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => Cache.Data = default;

    public static bool Initialize(ref SystemState state)
    {
        bool server = (state.WorldUnmanaged.Flags & WorldFlags.GameServer) == WorldFlags.GameServer;
        bool client = (state.WorldUnmanaged.Flags & WorldFlags.GameClient) == WorldFlags.GameClient;
        if (!server && (client || (state.WorldUnmanaged.Flags & WorldFlags.Game) == 0)) return false;
        if (!server && Cache.Data.Initialized && Cache.Data.IsServer) return false;

        var query = state.GetEntityQuery(ComponentType.ReadWrite<MatchState>());
        if (server && query.IsEmptyIgnoreFilter) return false;
        if (query.IsEmptyIgnoreFilter)
            state.EntityManager.CreateSingleton(new MatchState { Phase = MatchPhase.Preparing });
        var phase = query.GetSingleton<MatchState>().Phase;
        Cache.Data = new CachedState
        {
            Owner = state.WorldUnmanaged.SequenceNumber,
            Phase = phase,
            Initialized = true,
            IsServer = server
        };
        return true;
    }

    /// <summary>Updates the authoritative ghost state first, then the server cache.</summary>
    public static bool SetPhase(ref SystemState state, MatchPhase phase)
    {
        if (!Cache.Data.Initialized || Cache.Data.Owner != state.WorldUnmanaged.SequenceNumber)
            return false;
        if (phase < MatchPhase.Preparing || phase > MatchPhase.Finished)
            return false;

        // SystemState caches this query; it is only read on transitions, not every gameplay tick.
        var query = state.GetEntityQuery(ComponentType.ReadWrite<MatchState>());
        var entity = query.GetSingletonEntity();
        state.EntityManager.SetComponentData(entity, new MatchState { Phase = phase });
        Cache.Data.Phase = phase;
        return true;
    }

    public static void Shutdown(ref SystemState state)
    {
        // An old/local world being disposed must not clear a newer server session.
        if (Cache.Data.Initialized && Cache.Data.Owner == state.WorldUnmanaged.SequenceNumber)
            Cache.Data = default;
    }
}
