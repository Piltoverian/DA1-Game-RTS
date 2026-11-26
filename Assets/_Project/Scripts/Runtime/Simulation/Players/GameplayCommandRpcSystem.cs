using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

public struct GameplayCommandRpc : IRpcCommand
{
    public ulong MatchSessionId;
    public uint JoinGeneration, Epoch, RequestId;
    public CommandType Type;
    public int SourceId, TargetId;
    public ulong KnownNeutralId;
    public NetworkTick SourceTick, TargetTick;
    public float3 Position;
    public FixedString64Bytes DefinitionId;
    public ProductionKind JobKind;
    public bool Cancel;
    public int MemberIndex, MemberCount;
}
public enum CommandReceipt : byte { Queued, InvalidSession, InvalidCommand, NotOwner, InvalidTarget, PlacementExpired, RateLimited }
public struct GameplayCommandResultRpc : IRpcCommand
{
    public uint RequestId;
    public CommandReceipt Result;
    // One bounded overload response explicitly rejects all undrained requests on this transport.
    public bool RejectPending;
}
public struct ClientCommandStatus : IComponentData
{
    public uint LastRequestId;
    public CommandReceipt Result;
    public uint NextRequestId;
}

public static class GameplayCommandNetwork
{
    public static bool TryCreate(EntityManager em, out Entity connection, out GameplayCommandRpc command)
    {
        command = default;
        if (!PlayerNetwork.TryLocal(em.World, out connection, out var player)) return false;
        using var match = em.CreateEntityQuery(typeof(MatchState));
        if (!match.TryGetSingleton<MatchState>(out var phase) || phase.Phase != MatchPhase.Running) return false;
        var status = em.HasComponent<ClientCommandStatus>(connection) ? em.GetComponentData<ClientCommandStatus>(connection) : default;
        status.NextRequestId++;
        if (status.NextRequestId == 0) status.NextRequestId++;
        if (em.HasComponent<ClientCommandStatus>(connection)) em.SetComponentData(connection, status);
        else em.AddComponentData(connection, status);
        command = new GameplayCommandRpc { MatchSessionId = player.MatchSessionId, JoinGeneration = player.JoinGeneration,
            Epoch = player.Epoch, RequestId = status.NextRequestId };
        return true;
    }

    static bool TryGhost(EntityManager em, Entity entity, out GhostInstance ghost)
    {
        ghost = default;
        if (!em.Exists(entity) || em.HasComponent<Prefab>(entity) || !em.HasComponent<GhostInstance>(entity)) return false;
        ghost = em.GetComponentData<GhostInstance>(entity);
        return ghost.ghostId != 0;
    }

    public static void Send(EntityManager em, Entity source, CommandType type, GameBlobReference progression,
        Entity target, float3 position, bool cancel = false)
    {
        if (!TryGhost(em, source, out var sourceGhost) || !TryCreate(em, out var connection, out var command)) return;
        command.Type = type; command.SourceId = sourceGhost.ghostId; command.SourceTick = sourceGhost.spawnTick;
        command.Position = position; command.Cancel = cancel;
        if (type == CommandType.Progression)
        {
            if (progression.CatalogKey == RegistryTypeKey.For<TrainBlob>()) command.JobKind = ProductionKind.Train;
            else if (progression.CatalogKey == RegistryTypeKey.For<ResearchBlob>()) command.JobKind = ProductionKind.Research;
            else return;
            command.DefinitionId = progression.ID;
        }
        if (target != Entity.Null)
        {
            if (!TryGhost(em, target, out var targetGhost)) return;
            command.TargetId = targetGhost.ghostId; command.TargetTick = targetGhost.spawnTick;
            if (em.HasComponent<LocalTransform>(target)) command.Position = em.GetComponentData<LocalTransform>(target).Position;
        }
        PlayerNetwork.Send(em, connection, command);
    }

    public static void SendPlacement(EntityManager em, IReadOnlyList<Entity> sources, Entity prefab, float3 position)
    {
        if (sources.Count == 0 || sources.Count > 4096 || !em.HasComponent<BuildingComponent>(prefab)) return;
        var keys = new List<GhostInstance>();
        foreach (var source in sources) if (TryGhost(em, source, out var key)) keys.Add(key);
        if (keys.Count == 0 || !TryCreate(em, out var connection, out var command)) return;
        command.Type = CommandType.PlanBuild; command.DefinitionId = em.GetComponentData<BuildingComponent>(prefab).DefinitionID;
        command.Position = position; command.MemberCount = keys.Count;
        for (int i = 0; i < keys.Count; i++)
        {
            command.MemberIndex = i; command.SourceId = keys[i].ghostId; command.SourceTick = keys[i].spawnTick;
            PlayerNetwork.Send(em, connection, command);
        }
    }

    public static void SendKnownNeutral(EntityManager em, Entity source, ulong neutralId)
    {
        if (neutralId == 0 || !TryGhost(em, source, out var ghost) || !TryCreate(em, out var connection, out var command)) return;
        command.Type = CommandType.TargetTo; command.SourceId = ghost.ghostId; command.SourceTick = ghost.spawnTick;
        command.KnownNeutralId = neutralId;
        PlayerNetwork.Send(em, connection, command);
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(CommandQueue))]
[UpdateBefore(typeof(BuildingCancelSystem))]
[UpdateBefore(typeof(NetworkBuildOrderSystem))]
public partial class GameplayCommandServerSystem : SystemBase
{
    sealed class Placement
    {
        public GameplayCommandRpc Command;
        public Entity Order;
        public double Expires;
    }
    sealed class ConnectionBudget
    {
        public double Updated;
        public double Orders = 8, Members = 8192;
        public uint RetiredThrough;
        public readonly Queue<uint> History = new();
    }
    readonly Dictionary<(Entity Connection, uint Request), Placement> placements = new();
    readonly Dictionary<Entity, ConnectionBudget> budgets = new();
    readonly Dictionary<Entity, Queue<GameplayCommandRpc>> pending = new();
    public const int MaxCommandsPerUpdate = 4096, MaxPendingCommands = 8192;
    uint currentEpoch;
    bool hasRoster;
    const int MaxHistory = 256, MaxOpenGroups = 4;
    const double AdmissionSeconds = 10;
    protected override void OnCreate() { RequireForUpdate<CommandQueueComponent>(); RequireForUpdate<MatchState>(); }

    protected override void OnUpdate()
    {
        using var rosterQuery = EntityManager.CreateEntityQuery(typeof(PersistentPlayerSlot));
        hasRoster = rosterQuery.CalculateEntityCount() == 1;
        using var terrainQuery = EntityManager.CreateEntityQuery(typeof(TerrainVisualSource));
        currentEpoch = terrainQuery.CalculateEntityCount() == 1
            ? EntityManager.GetComponentObject<TerrainVisualSource>(terrainQuery.GetSingletonEntity()).Epoch : 0;
        var disconnected = new List<Entity>();
        foreach (var pair in budgets)
            if (!EntityManager.Exists(pair.Key) || EntityManager.HasComponent<NetworkStreamRequestDisconnect>(pair.Key)) disconnected.Add(pair.Key);
        foreach (var connection in disconnected)
        {
            foreach (var id in budgets[connection].History) placements.Remove((connection, id));
            budgets.Remove(connection);
        }
        disconnected.Clear();
        foreach (var pair in pending)
            if (!EntityManager.Exists(pair.Key) || EntityManager.HasComponent<NetworkStreamRequestDisconnect>(pair.Key)
                || EntityManager.HasComponent<PlayerJoinDenied>(pair.Key)) disconnected.Add(pair.Key);
        foreach (var connection in disconnected) pending.Remove(connection);
        using var ghostsQuery = EntityManager.CreateEntityQuery(typeof(GhostInstance));
        using var ghostEntities = ghostsQuery.ToEntityArray(Allocator.Temp);
        var ghosts = new Dictionary<SpawnedGhost, Entity>();
        foreach (var entity in ghostEntities)
        {
            var value = EntityManager.GetComponentData<GhostInstance>(entity);
            if (value.ghostId != 0) ghosts[new SpawnedGhost { ghostId = value.ghostId, spawnTick = value.spawnTick }] = entity;
        }
        using var query = EntityManager.CreateEntityQuery(typeof(GameplayCommandRpc), typeof(ReceiveRpcCommandRequest));
        using var requests = query.ToEntityArray(Allocator.Temp);
        foreach (var request in requests)
        {
            var command = EntityManager.GetComponentData<GameplayCommandRpc>(request);
            var connection = EntityManager.GetComponentData<ReceiveRpcCommandRequest>(request).SourceConnection;
            if (EntityManager.Exists(connection) && EntityManager.HasComponent<PlayerConnection>(connection)
                && !EntityManager.HasComponent<NetworkStreamRequestDisconnect>(connection)
                && !EntityManager.HasComponent<PlayerJoinDenied>(connection))
            {
                if (!pending.TryGetValue(connection, out var queue)) pending.Add(connection, queue = new Queue<GameplayCommandRpc>());
                if (queue.Count < MaxPendingCommands) queue.Enqueue(command);
                else
                {
                    queue.Clear();
                    PlayerNetwork.Send(EntityManager, connection, new GameplayCommandResultRpc
                    { RequestId = command.RequestId, Result = CommandReceipt.RateLimited, RejectPending = true });
                    EntityManager.AddComponentData(connection, new PlayerJoinDenied { DisconnectAfter = PlayerProtocolClock.Now(EntityManager) + .5 });
                }
            }
            EntityManager.DestroyEntity(request);
        }
        foreach (var pair in pending)
            for (int i = 0; i < MaxCommandsPerUpdate && pair.Value.Count > 0; i++) Process(pair.Key, pair.Value.Dequeue(), ghosts);
    }

    void Process(Entity connection, GameplayCommandRpc command, Dictionary<SpawnedGhost, Entity> ghosts)
    {
        var em = EntityManager;
        if (!em.Exists(connection) || !em.HasComponent<PlayerConnection>(connection)
            || em.HasComponent<NetworkStreamRequestDisconnect>(connection)) return;
        var player = em.GetComponentData<PlayerConnection>(connection);
        var ready = em.HasComponent<PlayerReady>(connection) ? em.GetComponentData<PlayerReady>(connection) : default;
        if (command.MatchSessionId != player.MatchSessionId || command.JoinGeneration != player.JoinGeneration
            || command.Epoch != player.Epoch || command.Epoch != currentEpoch
            || ready.MatchSessionId != player.MatchSessionId || ready.JoinGeneration != player.JoinGeneration || ready.Epoch != player.Epoch
            || SystemAPI.GetSingleton<MatchState>().Phase != MatchPhase.Running
            || hasRoster && !PlayerSessionSlots.IsCurrent(em, connection, player))
        { Reply(connection, command.RequestId, CommandReceipt.InvalidSession); return; }
        if (command.RequestId == 0 || !math.all(math.isfinite(command.Position)) || command.Type < CommandType.Move || command.Type > CommandType.PlanBuild)
        { Reply(connection, command.RequestId, CommandReceipt.InvalidCommand); return; }
        if (!ghosts.TryGetValue(new SpawnedGhost { ghostId = command.SourceId, spawnTick = command.SourceTick }, out var source)
            || !CommandAuthority.CanControl(em, player.PlayerId, source)
            || (em.HasComponent<EntityHealth>(source) && em.GetComponentData<EntityHealth>(source).CurrentHP <= 0))
        { Reply(connection, command.RequestId, CommandReceipt.NotOwner); return; }
        if (command.RequestId != 0 && em.HasComponent<NetworkWorkerCommand>(source))
        {
            var last = em.GetComponentData<NetworkWorkerCommand>(source);
            if (last.Connection == connection && last.JoinGeneration == command.JoinGeneration)
            {
                if (NetworkBuildOrders.IsOlder(command.RequestId, last.RequestId))
                { Reply(connection, command.RequestId, CommandReceipt.InvalidCommand); return; }
                if (command.RequestId == last.RequestId && command.Type != CommandType.PlanBuild)
                { Reply(connection, command.RequestId, CommandReceipt.Queued); return; }
            }
        }
        if (command.Cancel)
        {
            if (!em.HasComponent<BuildingConstruction>(source)) { Reply(connection, command.RequestId, CommandReceipt.InvalidCommand); return; }
            MarkSource(connection, command, source);
            var cancel = em.CreateEntity();
            em.AddComponentData(cancel, new CancelBuildingRequest { BuildingEntity = source, PlayerId = player.PlayerId });
            Reply(connection, command.RequestId, CommandReceipt.Queued); return;
        }
        if (command.Type == CommandType.PlanBuild)
        {
            HandlePlacement(connection, command, source, player.PlayerId); return;
        }
        Entity target = Entity.Null;
        FogKnownTarget known = default;
        if (command.KnownNeutralId != 0)
        {
            if (command.Type != CommandType.TargetTo || !NeutralMemoryServerSystem.TryGetKnownTarget(World, player.PlayerId, command.KnownNeutralId, out known)
                || !known.HasValue || known.Kind != FogRenderKind.Resource)
            { Reply(connection, command.RequestId, CommandReceipt.InvalidTarget); return; }
        }
        else if (command.Type == CommandType.TargetTo || command.Type == CommandType.Build)
        {
            if (!ghosts.TryGetValue(new SpawnedGhost { ghostId = command.TargetId, spawnTick = command.TargetTick }, out target)
                || !GameplayGhostVisibility.TryGetFog(em, out var grid, out var fog)
                || !GameplayGhostVisibility.CanReceive(em, target, player.PlayerId,
                    em.GetComponentData<NetworkId>(connection).Value, grid, fog, true))
            { Reply(connection, command.RequestId, CommandReceipt.InvalidTarget); return; }
        }
        if (command.Type == CommandType.Move && !em.HasComponent<MoveOverride>(source))
        { Reply(connection, command.RequestId, CommandReceipt.InvalidCommand); return; }
        if (command.Type == CommandType.Progression && (command.DefinitionId.IsEmpty
            || (command.JobKind != ProductionKind.Train && command.JobKind != ProductionKind.Research)))
        { Reply(connection, command.RequestId, CommandReceipt.InvalidCommand); return; }
        var progression = new GameBlobReference { ID = command.DefinitionId,
            CatalogKey = command.JobKind == ProductionKind.Train ? RegistryTypeKey.For<TrainBlob>() : RegistryTypeKey.For<ResearchBlob>() };
        MarkSource(connection, command, source);
        CommandDataHelper.AddCommandToQueue(em, player.PlayerId, source, command.Type, progression, target,
            known.HasValue ? known.Position : command.Position, knownTarget: known);
        Reply(connection, command.RequestId, CommandReceipt.Queued);
    }

    void MarkSource(Entity connection, GameplayCommandRpc command, Entity source)
    {
        if (command.RequestId == 0) return; // Legacy local/test commands have no network sequence.
        var value = new NetworkWorkerCommand { Connection = connection, JoinGeneration = command.JoinGeneration, RequestId = command.RequestId };
        if (EntityManager.HasComponent<NetworkWorkerCommand>(source)) EntityManager.SetComponentData(source, value);
        else EntityManager.AddComponentData(source, value);
    }

    void HandlePlacement(Entity connection, GameplayCommandRpc command, Entity source, int playerId)
    {
        if (command.RequestId == 0 || command.MemberCount < 1 || command.MemberCount > 4096 || command.MemberIndex < 0
            || command.MemberIndex >= command.MemberCount || command.DefinitionId.IsEmpty)
        { Reply(connection, command.RequestId, CommandReceipt.InvalidCommand); return; }
        double now = SystemAPI.Time.ElapsedTime;
        if (!budgets.TryGetValue(connection, out var budget))
            budgets.Add(connection, budget = new ConnectionBudget { Updated = now });
        double elapsed = System.Math.Max(0, now - budget.Updated);
        budget.Orders = System.Math.Min(8, budget.Orders + elapsed * 4);
        budget.Members = System.Math.Min(8192, budget.Members + elapsed * 2048);
        budget.Updated = now;
        if (budget.Members < 1) { Reply(connection, command.RequestId, CommandReceipt.RateLimited); return; }
        budget.Members--;
        var key = (connection, command.RequestId);
        if (!placements.TryGetValue(key, out var placement))
        {
            if (budget.RetiredThrough != 0 && !NetworkBuildOrders.IsOlder(budget.RetiredThrough, command.RequestId))
            { Reply(connection, command.RequestId, CommandReceipt.PlacementExpired); return; }
            int open = 0, active = 0;
            using (var orderQuery = EntityManager.CreateEntityQuery(typeof(NetworkBuildOrder)))
            using (var liveOrders = orderQuery.ToComponentDataArray<NetworkBuildOrder>(Allocator.Temp))
                foreach (var live in liveOrders)
                    if (live.Connection == connection && (live.Phase == NetworkBuildPhase.Planning || live.Phase == NetworkBuildPhase.Placing)) active++;
            foreach (uint id in budget.History)
            {
                if (!placements.TryGetValue((connection, id), out var old) || !EntityManager.Exists(old.Order)) continue;
                if (old.Expires > now && EntityManager.GetBuffer<NetworkBuildMember>(old.Order).Length < old.Command.MemberCount) open++;
            }
            if (budget.Orders < 1 || open >= MaxOpenGroups || active >= 64)
            { Reply(connection, command.RequestId, CommandReceipt.RateLimited); return; }
            Entity prefab = Entity.Null;
            if (ProductionJobs.TryRegistry(EntityManager, out var registry))
            {
                var definition = EntityManager.GetBuffer<RegistryBlobElement>(registry, true).GetBlobByID<BuildingBlob>(command.DefinitionId, out var found);
                if (found == FunctionResult.Success)
                {
                    var prefabs = EntityManager.GetBuffer<RegistryPrefabElement>(registry, true);
                    int index = definition.Value.Base.PrefabIndex;
                    if (index >= 0 && index < prefabs.Length) prefab = prefabs[index].Prefab;
                }
            }
            var order = new NetworkBuildOrder { Connection = connection, PlayerId = playerId, RequestId = command.RequestId,
                JoinGeneration = command.JoinGeneration, Prefab = prefab, Position = command.Position, AdmissionUntil = now + AdmissionSeconds };
            if (!NetworkBuildOrders.ValidWorker(EntityManager, source, order))
            { Reply(connection, command.RequestId, CommandReceipt.InvalidCommand); return; }
            var entity = EntityManager.CreateEntity(typeof(NetworkBuildOrder), typeof(NetworkBuildMember));
            EntityManager.SetComponentData(entity, order);
            placement = new Placement { Command = command, Order = entity, Expires = order.AdmissionUntil };
            placements.Add(key, placement); budget.Orders--; budget.History.Enqueue(command.RequestId);
            while (budget.History.Count > MaxHistory)
            {
                uint retired = budget.History.Dequeue();
                placements.Remove((connection, retired));
                if (budget.RetiredThrough == 0 || NetworkBuildOrders.IsOlder(budget.RetiredThrough, retired)) budget.RetiredThrough = retired;
            }
        }
        if (placement.Command.DefinitionId != command.DefinitionId || placement.Command.MemberCount != command.MemberCount
            || math.any(placement.Command.Position != command.Position) || placement.Command.JoinGeneration != command.JoinGeneration
            || placement.Command.MatchSessionId != command.MatchSessionId || placement.Command.Epoch != command.Epoch)
        { Reply(connection, command.RequestId, CommandReceipt.InvalidCommand); return; }
        if (now > placement.Expires || !EntityManager.Exists(placement.Order))
        { Reply(connection, command.RequestId, CommandReceipt.PlacementExpired); return; }
        var current = EntityManager.GetComponentData<NetworkBuildOrder>(placement.Order);
        if (current.Phase == NetworkBuildPhase.Failed || (current.Phase == NetworkBuildPhase.Site && !NetworkBuildOrders.CanContinue(EntityManager, current))
            || !NetworkBuildOrders.ValidWorker(EntityManager, source, current))
        { Reply(connection, command.RequestId, CommandReceipt.InvalidCommand); return; }
        var members = EntityManager.GetBuffer<NetworkBuildMember>(placement.Order);
        foreach (var member in members)
        {
            if (member.Index == command.MemberIndex || member.Worker == source)
            {
                Reply(connection, command.RequestId, member.Index == command.MemberIndex && member.Worker == source
                    ? CommandReceipt.Queued : CommandReceipt.InvalidCommand);
                return;
            }
        }
        members.Add(new NetworkBuildMember { Index = command.MemberIndex, Worker = source });
        MarkSource(connection, command, source);
        CommandDataHelper.AddPlanBuildToQueue(EntityManager, playerId, new[] { source }, current.Prefab, current.Position, placement.Order);
        Reply(connection, command.RequestId, CommandReceipt.Queued);
    }

    void Reply(Entity connection, uint request, CommandReceipt result) =>
        PlayerNetwork.Send(EntityManager, connection, new GameplayCommandResultRpc { RequestId = request, Result = result });
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class GameplayCommandResultSystem : SystemBase
{
    protected override void OnUpdate()
    {
        using var query = EntityManager.CreateEntityQuery(typeof(GameplayCommandResultRpc), typeof(ReceiveRpcCommandRequest));
        using var requests = query.ToEntityArray(Allocator.Temp);
        foreach (var request in requests)
        {
            var result = EntityManager.GetComponentData<GameplayCommandResultRpc>(request);
            var connection = EntityManager.GetComponentData<ReceiveRpcCommandRequest>(request).SourceConnection;
            if (EntityManager.Exists(connection) && EntityManager.HasComponent<ClientCommandStatus>(connection))
            {
                var status = EntityManager.GetComponentData<ClientCommandStatus>(connection);
                status.LastRequestId = result.RequestId; status.Result = result.Result;
                EntityManager.SetComponentData(connection, status);
                if (result.Result != CommandReceipt.Queued) Debug.LogWarning(result.RejectPending
                    ? "Pending commands rejected: RateLimited; transport overloaded." : $"Command {result.RequestId}: {result.Result}");
            }
            EntityManager.DestroyEntity(request);
        }
    }
}
