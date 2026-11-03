using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

public struct NeutralMemoryHeaderRpc : IRpcCommand
{
    public ulong MatchSessionId, CutoffRevision;
    public uint JoinGeneration, Epoch, StreamGeneration;
    public int Count;
}

public struct NeutralMemoryDoneRpc : IRpcCommand
{
    public ulong MatchSessionId, CutoffRevision;
    public uint JoinGeneration, Epoch, StreamGeneration;
    public int Count;
}

// Plain fields intentionally avoid simulation components and collection serializers in the wire format.
public struct NeutralMemoryEntryRpc : IRpcCommand
{
    public ulong MatchSessionId, NeutralId, Revision;
    public NetworkTick ObservedTick;
    public uint JoinGeneration, Epoch, StreamGeneration;
    public int SnapshotIndex, TemplateGhostType;
    public bool Removed;
    public FixedString64Bytes DefinitionId;
    public FogRenderKind Kind;
    public float3 Position;
    public quaternion Rotation;
    public float Scale;
    public float4 Post0, Post1, Post2, Post3;
    public int2 FootprintMin, FootprintMax;
    public bool HasHealth, HasHealthMax, HasResource, HasAbility;
    public float Health, HealthMax, ConstructionProgress;
    public int Amount;
    public ResourceType ResourceType;
    public ConstructionPhase ConstructionPhase;
    public FogAbilityKind AbilityKind;
    public byte AbilityState;
    public FixedString64Bytes AbilityId;

    public static NeutralMemoryEntryRpc From(in NeutralMemoryRecord record)
    {
        var data = record.Data;
        return new NeutralMemoryEntryRpc { NeutralId = record.Id, Revision = record.Revision, ObservedTick = record.ObservedTick,
            Removed = record.Removed, TemplateGhostType = record.TemplateGhostType, ResourceType = record.ResourceType,
            DefinitionId = data.GameRegId ?? default, Kind = data.Kind,
            Position = data.Transform.Position, Rotation = data.Transform.Rotation, Scale = data.Transform.Scale,
            Post0 = data.PostTransform.c0, Post1 = data.PostTransform.c1, Post2 = data.PostTransform.c2, Post3 = data.PostTransform.c3,
            FootprintMin = data.FootprintMin, FootprintMax = data.FootprintMax,
            HasHealth = data.Health.HasValue, Health = data.Health?.Current ?? 0,
            HasHealthMax = data.Health?.MaxOverride.HasValue ?? false, HealthMax = data.Health?.MaxOverride ?? 0,
            HasResource = data.Resource.HasValue, Amount = data.Resource?.Amount ?? 0,
            ConstructionPhase = data.ConstructionPhase, ConstructionProgress = data.ConstructionProgress,
            HasAbility = data.ActiveAbility.HasValue, AbilityKind = data.ActiveAbility?.Kind ?? default,
            AbilityState = data.ActiveAbility?.State ?? 0, AbilityId = data.ActiveAbility?.Id ?? default };
    }

    public NeutralMemoryRecord ToRecord() => new NeutralMemoryRecord {
        Id = NeutralId, Revision = Revision, ObservedTick = ObservedTick, Removed = Removed,
        TemplateGhostType = TemplateGhostType, ResourceType = ResourceType,
        Data = new FogEntityRenderData { NeutralId = NeutralId, OwnerId = -1, Kind = Kind,
            GameRegId = DefinitionId.IsEmpty ? null : DefinitionId,
            Transform = LocalTransform.FromPositionRotationScale(Position, Rotation, Scale), PostTransform = new float4x4(Post0, Post1, Post2, Post3),
            FootprintMin = FootprintMin, FootprintMax = FootprintMax,
            IsStatic = Kind == FogRenderKind.Resource || Kind == FogRenderKind.Building,
            Health = HasHealth ? new FogHealthSnapshot { Current = Health, MaxOverride = HasHealthMax ? HealthMax : null } : null,
            Resource = HasResource ? new FogResourceSnapshot { Amount = Amount } : null,
            ConstructionPhase = ConstructionPhase, ConstructionProgress = ConstructionProgress,
            ActiveAbility = HasAbility ? new FogActiveAbilitySnapshot { Kind = AbilityKind, State = AbilityState, Id = AbilityId } : null }
    };
}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PlayerHandshakeServerSystem))]
[UpdateBefore(typeof(GhostSendSystem))]
public partial class NeutralMemoryServerSystem : SystemBase
{
    public const int RpcBudgetPerFrame = 32;
    sealed class Stream
    {
        public PlayerConnection Player;
        public uint Generation;
        public ulong Cutoff;
        public bool HeaderSent, Done;
        public int Cursor;
        public readonly List<NeutralMemoryRecord> Snapshot = new();
        public readonly Dictionary<ulong, ulong> Sent = new();
    }
    readonly Dictionary<int, NeutralMemoryStore> players = new();
    readonly Dictionary<Entity, Stream> streams = new();
    readonly Dictionary<Entity, ulong> localIds = new();
    readonly List<NeutralMemoryRecord> observations = new();
    readonly List<Entity> staleStreams = new();
    FogOfWarFrame frame;
    Entity gridEntity;
    uint gridRevision, nextStream;
    ulong session, nextLocalId;

    public IReadOnlyDictionary<int, NeutralMemoryStore> Players => players;

    protected override void OnCreate() { frame = new FogOfWarFrame(); }
    protected override void OnDestroy() { frame.Dispose(); }

    public static bool TryGetKnownTarget(World world, int player, ulong id, out FogKnownTarget known)
    {
        known = default;
        var system = world?.GetExistingSystemManaged<NeutralMemoryServerSystem>();
        return system != null && system.players.TryGetValue(player, out var store)
            && FogOrderRules.TryGrid(world.EntityManager, out _, out var grid) && store.TryKnownTarget(id, grid, out known);
    }

    protected override void OnUpdate()
    {
        EntityManager.CompleteAllTrackedJobs();
        // An incomplete FOW tick preserves the last complete memory and never produces tombstones.
        if (!GameplayGhostVisibility.TryGetFog(EntityManager, out _, out var fog)) return;
        using var matches = EntityManager.CreateEntityQuery(typeof(MatchSession));
        ulong currentSession = matches.TryGetSingleton<MatchSession>(out var match) ? match.Id : 0;
        if (gridEntity != fog.SourceGrid || gridRevision != fog.GridRevision || session != currentSession)
        {
            players.Clear(); streams.Clear(); localIds.Clear(); nextLocalId = 0;
            gridEntity = fog.SourceGrid; gridRevision = fog.GridRevision; session = currentSession;
        }
        using var contexts = EntityManager.CreateEntityQuery(typeof(PlayerContext));
        using var times = EntityManager.CreateEntityQuery(typeof(NetworkTime));
        NetworkTick tick = times.TryGetSingleton<NetworkTime>(out var networkTime) ? networkTime.ServerTick : default;
        using var contextData = contexts.ToComponentDataArray<PlayerContext>(Allocator.Temp);
        foreach (var context in contextData)
        {
            if (context.PlayerId < 0) continue;
            if (!players.TryGetValue(context.PlayerId, out var memory))
                players.Add(context.PlayerId, memory = new NeutralMemoryStore());
            if (!FogOfWarSnapshot.Collect(EntityManager, context.PlayerId, frame)) continue;
            observations.Clear();
            foreach (var data in frame.Entities)
            {
                ulong id = 0;
                int template = -1;
                if (EntityManager.HasComponent<GhostInstance>(data.Entity))
                {
                    var ghost = EntityManager.GetComponentData<GhostInstance>(data.Entity);
                    id = NeutralMemoryIdentity.FromGhost(ghost); template = ghost.ghostType;
                }
                if (id == 0 && !World.IsServer())
                {
                    if (!localIds.TryGetValue(data.Entity, out id)) localIds[data.Entity] = id = ++nextLocalId;
                }
                // An unassigned network ghost is deferred until it has its authoritative spawn key.
                if (id == 0 || data.Kind == FogRenderKind.Projectile) continue;
                bool neutral = NeutralMemoryIdentity.IsNeutralGameplay(EntityManager, data.Entity);
                if (!neutral && !memory.Records.ContainsKey(id)) continue;
                observations.Add(new NeutralMemoryRecord { Id = id, Data = data, TemplateGhostType = template,
                    ResourceType = EntityManager.HasComponent<ResourceNodeData>(data.Entity)
                        ? EntityManager.GetComponentData<ResourceNodeData>(data.Entity).Type : default });
            }
            memory.Observe(frame, observations, tick);
        }
        if (World.IsServer()) SendStreams();
    }

    void SendStreams()
    {
        staleStreams.Clear();
        foreach (var pair in streams)
            if (!EntityManager.Exists(pair.Key) || !EntityManager.HasComponent<PlayerConnection>(pair.Key)
                || EntityManager.HasComponent<NetworkStreamRequestDisconnect>(pair.Key)) staleStreams.Add(pair.Key);
        foreach (var connection in staleStreams) streams.Remove(connection);
        using var connections = EntityManager.CreateEntityQuery(typeof(NetworkId), typeof(NetworkStreamInGame), typeof(PlayerConnection));
        using var entities = connections.ToEntityArray(Allocator.Temp);
        foreach (var connection in entities)
        {
            var player = EntityManager.GetComponentData<PlayerConnection>(connection);
            if (EntityManager.HasComponent<NetworkStreamRequestDisconnect>(connection) || player.Epoch == 0
                || !PlayerSessionSlots.IsCurrent(EntityManager, connection, player)
                || !players.TryGetValue(player.PlayerId, out var memory)) continue;
            if (!streams.TryGetValue(connection, out var stream) || stream.Player.MatchSessionId != player.MatchSessionId
                || stream.Player.JoinGeneration != player.JoinGeneration || stream.Player.Epoch != player.Epoch)
            {
                stream = new Stream { Player = player, Generation = ++nextStream, Cutoff = memory.Revision };
                if (stream.Generation == 0) stream.Generation = ++nextStream;
                foreach (var record in memory.Records.Values) stream.Snapshot.Add(record);
                streams[connection] = stream;
            }
            int budget = RpcBudgetPerFrame;
            if (!stream.HeaderSent)
            {
                PlayerNetwork.Send(EntityManager, connection, new NeutralMemoryHeaderRpc { MatchSessionId = player.MatchSessionId,
                    JoinGeneration = player.JoinGeneration, Epoch = player.Epoch, StreamGeneration = stream.Generation,
                    CutoffRevision = stream.Cutoff, Count = stream.Snapshot.Count });
                stream.HeaderSent = true; budget--;
            }
            // New observations get a bounded share without ever restarting the immutable snapshot.
            int deltaBudget = stream.Done ? budget : math.min(8, budget);
            foreach (var record in memory.Records.Values)
            {
                if (deltaBudget == 0) break;
                if (record.Revision <= stream.Cutoff || (stream.Sent.TryGetValue(record.Id, out var sent) && sent >= record.Revision)) continue;
                SendEntry(connection, stream, record, -1); budget--; deltaBudget--;
            }
            while (budget > 0 && stream.Cursor < stream.Snapshot.Count)
            {
                SendEntry(connection, stream, stream.Snapshot[stream.Cursor], stream.Cursor);
                stream.Cursor++; budget--;
            }
            if (!stream.Done && stream.Cursor == stream.Snapshot.Count && budget > 0)
            {
                PlayerNetwork.Send(EntityManager, connection, new NeutralMemoryDoneRpc { MatchSessionId = player.MatchSessionId,
                    JoinGeneration = player.JoinGeneration, Epoch = player.Epoch, StreamGeneration = stream.Generation,
                    CutoffRevision = stream.Cutoff, Count = stream.Snapshot.Count });
                stream.Done = true;
            }
        }
    }

    void SendEntry(Entity connection, Stream stream, in NeutralMemoryRecord record, int index)
    {
        var value = NeutralMemoryEntryRpc.From(record);
        value.MatchSessionId = stream.Player.MatchSessionId; value.JoinGeneration = stream.Player.JoinGeneration;
        value.Epoch = stream.Player.Epoch; value.StreamGeneration = stream.Generation; value.SnapshotIndex = index;
        PlayerNetwork.Send(EntityManager, connection, value);
        if (!stream.Sent.TryGetValue(record.Id, out var sent) || sent < record.Revision) stream.Sent[record.Id] = record.Revision;
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(PlayerHandshakeReceiveSystem))]
public partial class NeutralMemoryClientSystem : SystemBase
{
    public NeutralMemoryReplica Memory { get; } = new();
    Entity connection;
    ClientPlayerState identity;

    public static bool IsReady(World world, in ClientPlayerState player)
    {
        var receiver = world?.GetExistingSystemManaged<NeutralMemoryClientSystem>();
        return receiver != null && receiver.Memory.Ready && PlayerNetwork.Matches(player,
            receiver.Memory.Header.MatchSessionId, receiver.Memory.Header.JoinGeneration, receiver.Memory.Header.Epoch);
    }

    protected override void OnUpdate()
    {
        if (!PlayerNetwork.TryLocal(World, out var current, out var player, false)) { Memory.Clear(); connection = Entity.Null; return; }
        if (connection != current || !PlayerNetwork.Matches(identity, player.MatchSessionId, player.JoinGeneration, player.Epoch))
        { Memory.Clear(); connection = current; identity = player; }
        using var headers = EntityManager.CreateEntityQuery(typeof(NeutralMemoryHeaderRpc), typeof(ReceiveRpcCommandRequest));
        using var headerEntities = headers.ToEntityArray(Allocator.Temp);
        foreach (var request in headerEntities)
        {
            var value = EntityManager.GetComponentData<NeutralMemoryHeaderRpc>(request);
            if (Accept(request, value.MatchSessionId, value.JoinGeneration, value.Epoch)) Memory.Begin(value);
            EntityManager.DestroyEntity(request);
        }
        using var entries = EntityManager.CreateEntityQuery(typeof(NeutralMemoryEntryRpc), typeof(ReceiveRpcCommandRequest));
        using var entryEntities = entries.ToEntityArray(Allocator.Temp);
        foreach (var request in entryEntities)
        {
            var value = EntityManager.GetComponentData<NeutralMemoryEntryRpc>(request);
            if (Accept(request, value.MatchSessionId, value.JoinGeneration, value.Epoch))
            {
                if (value.StreamGeneration > Memory.Header.StreamGeneration) continue;
                Memory.Apply(value);
            }
            EntityManager.DestroyEntity(request);
        }
        using var ends = EntityManager.CreateEntityQuery(typeof(NeutralMemoryDoneRpc), typeof(ReceiveRpcCommandRequest));
        using var endEntities = ends.ToEntityArray(Allocator.Temp);
        foreach (var request in endEntities)
        {
            var value = EntityManager.GetComponentData<NeutralMemoryDoneRpc>(request);
            if (Accept(request, value.MatchSessionId, value.JoinGeneration, value.Epoch))
            {
                if (value.StreamGeneration > Memory.Header.StreamGeneration) continue;
                Memory.End(value);
            }
            EntityManager.DestroyEntity(request);
        }
    }

    bool Accept(Entity request, ulong match, uint join, uint epoch) =>
        EntityManager.GetComponentData<ReceiveRpcCommandRequest>(request).SourceConnection == connection
        && PlayerNetwork.Matches(identity, match, join, epoch);
}
