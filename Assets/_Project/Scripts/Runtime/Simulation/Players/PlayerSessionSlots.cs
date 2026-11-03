using System;
using System.Security.Cryptography;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

public enum PlayerSlotState : byte { AwaitingConnection, Loading, Ready, Disconnected }
public enum PlayerLoadingFailure : byte { None, AbsentAtStart, TransportDisconnected, LoadingTimedOut }
public enum PlayerLoadingProgress : byte { Admitted, ManifestSending, AwaitingClient, Ready }

public struct PlayerProtocolClockOverride : IComponentData { public double Now; }

public static class PlayerProtocolClock
{
    // Protocol deadlines continue while gameplay time is paused. The override belongs to one test world.
    public static double Now(EntityManager em)
    {
        using var query = em.CreateEntityQuery(typeof(PlayerProtocolClockOverride));
        return query.TryGetSingleton<PlayerProtocolClockOverride>(out var clock)
            ? clock.Now : UnityEngine.Time.realtimeSinceStartupAsDouble;
    }
}

// Server-only match state. Transport entities are disposable; this roster is not.
public struct PersistentPlayerSlot : IBufferElementData
{
    public int PlayerId;
    public Entity Context, Connection;
    public ulong MatchSessionId;
    public uint JoinGeneration;
    public bool Claimed;
    public PlayerSlotState State;
    public FixedString128Bytes TicketHash;
    public double LoadingStartedAt, LoadingDeadline, LastLoadingProgressAt;
    public PlayerLoadingProgress LoadingProgress;
    public PlayerLoadingFailure LoadingFailure;
}

public struct PlayerJoinTimeout : IComponentData
{
    public float Seconds;
    public float LoadingSeconds;
    public double Deadline;
    public bool Started;
}

public static class PlayerResumeTicket
{
    public static FixedString128Bytes Create()
    {
        var bytes = new byte[32];
        using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
        return new FixedString128Bytes(Convert.ToBase64String(bytes));
    }

    public static bool TryHash(FixedString128Bytes ticket, out FixedString128Bytes hash)
    {
        hash = default;
        if (ticket.Length != 44) return false;
        byte[] bytes;
        try { bytes = Convert.FromBase64String(ticket.ToString()); }
        catch (FormatException) { return false; }
        if (bytes.Length != 32) return false;
        using var sha = SHA256.Create();
        hash = new FixedString128Bytes(Convert.ToBase64String(sha.ComputeHash(bytes)));
        return true;
    }

    public static bool Matches(FixedString128Bytes ticket, FixedString128Bytes expectedHash)
    {
        if (!TryHash(ticket, out var hash) || expectedHash.Length != hash.Length) return false;
        int difference = 0;
        for (int i = 0; i < hash.Length; i++) difference |= hash[i] ^ expectedHash[i];
        return difference == 0;
    }
}

public static class PlayerSessionSlots
{
    public static bool Ensure(EntityManager em, ulong session, out Entity root)
    {
        root = Entity.Null;
        using var query = em.CreateEntityQuery(typeof(PlayerBootstrapSlot));
        if (session == 0 || query.CalculateEntityCount() != 1) return false;
        root = query.GetSingletonEntity();
        if (!em.HasBuffer<PersistentPlayerSlot>(root))
        {
            using var roster = em.GetBuffer<PlayerBootstrapSlot>(root, true).ToNativeArray(Allocator.Temp);
            var slots = em.AddBuffer<PersistentPlayerSlot>(root);
            foreach (var player in roster) slots.Add(new PersistentPlayerSlot
            { PlayerId = player.PlayerId, Context = player.Context, MatchSessionId = session });
        }
        if (!em.HasComponent<PlayerJoinTimeout>(root))
            em.AddComponentData(root, new PlayerJoinTimeout { Seconds = 30, LoadingSeconds = 60 });
        var current = em.GetBuffer<PersistentPlayerSlot>(root, true);
        foreach (var slot in current) if (slot.MatchSessionId != session) return false;
        return true;
    }

    public static bool TryGetSlot(EntityManager em, int playerId, out PersistentPlayerSlot slot)
    {
        slot = default;
        using var query = em.CreateEntityQuery(typeof(PersistentPlayerSlot));
        if (query.CalculateEntityCount() != 1) return false;
        foreach (var candidate in em.GetBuffer<PersistentPlayerSlot>(query.GetSingletonEntity(), true))
            if (candidate.PlayerId == playerId) { slot = candidate; return true; }
        return false;
    }

    public static bool IsCurrent(EntityManager em, Entity connection, in PlayerConnection player)
    {
        return em.Exists(connection) && em.HasComponent<NetworkId>(connection)
            && !em.HasComponent<NetworkStreamRequestDisconnect>(connection)
            && TryGetSlot(em, player.PlayerId, out var slot) && slot.Claimed && slot.Connection == connection
            && slot.MatchSessionId == player.MatchSessionId && slot.JoinGeneration == player.JoinGeneration;
    }

    public static bool IsReady(EntityManager em, in PersistentPlayerSlot slot, uint epoch)
    {
        if (!slot.Claimed || !em.Exists(slot.Connection) || !em.HasComponent<PlayerConnection>(slot.Connection)
            || !em.HasComponent<PlayerReady>(slot.Connection)) return false;
        var player = em.GetComponentData<PlayerConnection>(slot.Connection);
        var ready = em.GetComponentData<PlayerReady>(slot.Connection);
        return IsCurrent(em, slot.Connection, player) && player.Epoch == epoch
            && ready.MatchSessionId == slot.MatchSessionId && ready.JoinGeneration == slot.JoinGeneration && ready.Epoch == epoch;
    }

    public static bool AdvanceWaiting(EntityManager em, Entity root, double elapsed, uint epoch)
    {
        var timeout = em.GetComponentData<PlayerJoinTimeout>(root);
        if (!timeout.Started)
        {
            timeout.Started = true;
            timeout.Deadline = elapsed + (float.IsNaN(timeout.Seconds) || float.IsInfinity(timeout.Seconds) ? 30 : Math.Max(0, timeout.Seconds));
            em.SetComponentData(root, timeout);
        }
        using var slots = em.GetBuffer<PersistentPlayerSlot>(root, true).ToNativeArray(Allocator.Temp);
        bool allReady = slots.Length > 0;
        foreach (var slot in slots) allReady &= epoch != 0 && IsReady(em, slot, epoch);
        if (allReady) return true;
        if (elapsed < timeout.Deadline) return false;
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (epoch != 0 && IsReady(em, slot, epoch)) continue;
            // The roster deadline starts the match; an admitted transport owns its full loading window.
            if (em.Exists(slot.Connection) && em.HasComponent<NetworkId>(slot.Connection)
                && !em.HasComponent<NetworkStreamRequestDisconnect>(slot.Connection)) continue;
            slot.State = PlayerSlotState.Disconnected; slot.Connection = Entity.Null;
            slot.LoadingFailure = PlayerLoadingFailure.AbsentAtStart;
            var currentSlots = em.GetBuffer<PersistentPlayerSlot>(root);
            currentSlots[i] = slot;
        }
        return true;
    }

    public static void BeginLoading(EntityManager em, Entity root, ref PersistentPlayerSlot slot, double now)
    {
        var seconds = em.GetComponentData<PlayerJoinTimeout>(root).LoadingSeconds;
        if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0) seconds = 60;
        slot.State = PlayerSlotState.Loading;
        slot.LoadingStartedAt = slot.LastLoadingProgressAt = now;
        slot.LoadingDeadline = now + seconds;
        slot.LoadingProgress = PlayerLoadingProgress.Admitted;
        slot.LoadingFailure = PlayerLoadingFailure.None;
    }

    public static void RecordProgress(EntityManager em, Entity connection, PlayerLoadingProgress progress, double now)
    {
        using var query = em.CreateEntityQuery(typeof(PersistentPlayerSlot));
        if (query.CalculateEntityCount() != 1) return;
        var slots = em.GetBuffer<PersistentPlayerSlot>(query.GetSingletonEntity());
        for (int i = 0; i < slots.Length; i++)
            if (slots[i].Connection == connection)
            {
                var slot = slots[i]; slot.LoadingProgress = progress; slot.LastLoadingProgressAt = now;
                if (progress == PlayerLoadingProgress.Ready) slot.State = PlayerSlotState.Ready;
                slots[i] = slot; return;
            }
    }

    public static void AdvanceLoading(EntityManager em, Entity root, double now)
    {
        using var snapshot = em.GetBuffer<PersistentPlayerSlot>(root, true).ToNativeArray(Allocator.Temp);
        for (int i = 0; i < snapshot.Length; i++)
        {
            var slot = snapshot[i];
            if (!slot.Claimed || slot.Connection == Entity.Null) continue;
            var connection = slot.Connection;
            bool disconnected = !em.Exists(connection) || !em.HasComponent<NetworkId>(connection)
                || em.HasComponent<NetworkStreamRequestDisconnect>(connection);
            if (!disconnected && slot.State == PlayerSlotState.Loading)
            {
                if (slot.LoadingDeadline == 0) BeginLoading(em, root, ref slot, now);
                if (now >= slot.LoadingDeadline)
                {
                    slot.LoadingFailure = PlayerLoadingFailure.LoadingTimedOut;
                    if (em.HasComponent<PlayerReady>(connection)) em.RemoveComponent<PlayerReady>(connection);
                    em.AddComponent<NetworkStreamRequestDisconnect>(connection);
                    disconnected = true;
                }
            }
            if (disconnected)
            {
                slot.Connection = Entity.Null; slot.State = PlayerSlotState.Disconnected;
                if (slot.LoadingFailure == PlayerLoadingFailure.None) slot.LoadingFailure = PlayerLoadingFailure.TransportDisconnected;
            }
            var updatedSlots = em.GetBuffer<PersistentPlayerSlot>(root);
            updatedSlots[i] = slot;
        }
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(PlayerJoinServerSystem))]
public partial class PlayerSessionLifecycleSystem : SystemBase
{
    protected override void OnCreate() { RequireForUpdate<MatchSession>(); RequireForUpdate<PlayerBootstrapSlot>(); }
    protected override void OnUpdate()
    {
        if (!PlayerSessionSlots.Ensure(EntityManager, SystemAPI.GetSingleton<MatchSession>().Id, out var root)) return;
        using var rejected = EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<PlayerJoinDenied>() },
            None = new[] { ComponentType.ReadOnly<NetworkStreamRequestDisconnect>() }
        });
        using var denied = rejected.ToEntityArray(Allocator.Temp);
        double now = PlayerProtocolClock.Now(EntityManager);
        foreach (var connection in denied)
            if (now >= EntityManager.GetComponentData<PlayerJoinDenied>(connection).DisconnectAfter)
                EntityManager.AddComponent<NetworkStreamRequestDisconnect>(connection);
        PlayerSessionSlots.AdvanceLoading(EntityManager, root, now);
    }
}
