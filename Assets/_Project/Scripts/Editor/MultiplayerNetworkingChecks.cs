using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

// Can run in batch mode with -executeMethod MultiplayerNetworkingChecks.Run.
public static class MultiplayerNetworkingChecks
{
    static int checks;
    [MenuItem("Tools/RTS/Check Multiplayer Networking")]
    public static void Run()
    {
        checks = 0;
        CheckIdentity(); CheckVisibility(); CheckOwnership(); CheckBootstrap(); CheckOfflineContext(); CheckHandshakeOrder(); CheckCommands();
        Debug.Log($"Multiplayer networking checks passed: {checks} assertions.");
    }

    static void Assert(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(message);
    }

    static void CheckIdentity()
    {
        var player = new ClientPlayerState { MatchSessionId = 100, JoinGeneration = 3, Epoch = 5 };
        Assert(PlayerNetwork.Matches(player, 100, 3, 5), "Current receipt must match");
        Assert(!PlayerNetwork.Matches(player, 99, 3, 5), "Previous match must not match");
        Assert(!PlayerNetwork.Matches(player, 100, 2, 5), "Previous join must not match");
        Assert(!PlayerNetwork.Matches(player, 100, 3, 4), "Previous map epoch must not match");
        using var world = new World("Tag RPC check");
        var connection = world.EntityManager.CreateEntity();
        PlayerNetwork.Send(world.EntityManager, connection, new PlayerJoinRequest());
        using var request = world.EntityManager.CreateEntityQuery(typeof(PlayerJoinRequest), typeof(SendRpcCommandRequest));
        Assert(request.CalculateEntityCount() == 1, "Empty join RPC must not be written with SetComponentData");
    }

    static void CheckVisibility()
    {
        using var world = new World("Visibility checks");
        var em = world.EntityManager;
        var grid = new GridComponent { initialized = true, width = 4, height = 4, cellsize = 1 };
        var fog = new FogOfWarSpatialSystemData
        {
            Visible = new NativeParallelMultiHashMap<int, int>(16, Allocator.Temp),
            Explored = new NativeParallelMultiHashMap<int, int>(16, Allocator.Temp)
        };
        try
        {
            var resource = em.CreateEntity(typeof(ResourceNodeData), typeof(LocalTransform));
            em.SetComponentData(resource, LocalTransform.FromPosition(new float3(1.5f, 0, 1.5f)));
            Assert(!Visible(resource), "Unexplored resource must remain hidden");
            fog.Explored.Add(5, 1);
            Assert(!Visible(resource), "Explored does not authorize live resource updates");
            fog.Visible.Add(5, 1);
            Assert(Visible(resource), "Visible resource must be authorized");
            Assert(!GameplayGhostVisibility.CanReceive(em, resource, 2, 20, grid, fog, true), "FOW is per player");
            fog.Visible.Clear();
            Assert(!Visible(resource), "Lost visibility must revoke authorization");
            var unit = em.CreateEntity(typeof(UnitComponent), typeof(EntityOwner), typeof(LocalTransform));
            em.SetComponentData(unit, new EntityOwner { PlayerID = 1 });
            em.SetComponentData(unit, LocalTransform.FromPosition(new float3(1.5f, 0, 1.5f)));
            Assert(GameplayGhostVisibility.CanReceive(em, unit, 1, 10, grid, fog, false), "Own unit is independent of FOW readiness");
            em.SetComponentData(unit, new EntityOwner { PlayerID = 2 });
            Assert(!Visible(unit), "Hidden enemy must not replicate");
            fog.Visible.Add(5, 1);
            Assert(Visible(unit), "Visible enemy must replicate");
            Assert(!GameplayGhostVisibility.CanReceive(em, unit, 1, 10, grid, fog, false), "Uninitialized FOW must deny enemies");
            var terrain = em.CreateEntity(typeof(TerrainChunkGhost), typeof(GhostOwner));
            em.SetComponentData(terrain, new GhostOwner { NetworkId = 10 });
            Assert(Visible(terrain), "Terrain owner must receive it");
            Assert(!GameplayGhostVisibility.CanReceive(em, terrain, 1, 11, grid, fog, true), "Player ID cannot substitute for NetworkId");
            var context = em.CreateEntity(typeof(PlayerContext));
            em.SetComponentData(context, new PlayerContext { PlayerId = 1 });
            Assert(Visible(context), "Own context must replicate");
            Assert(!GameplayGhostVisibility.CanReceive(em, context, 2, 20, grid, fog, true), "Other player's context must stay private");
            em.AddComponent<Prefab>(unit);
            Assert(!Visible(unit), "Prefab templates are not live ghosts");
            bool Visible(Entity entity) => GameplayGhostVisibility.CanReceive(em, entity, 1, 10, grid, fog, true);
        }
        finally { fog.Visible.Dispose(); fog.Explored.Dispose(); }
    }

    static void CheckOwnership()
    {
        using var world = new World("Ownership checks", WorldFlags.GameServer);
        var em = world.EntityManager;
        var connection = em.CreateEntity(typeof(NetworkId), typeof(PlayerConnection));
        em.SetComponentData(connection, new NetworkId { Value = 17 });
        em.SetComponentData(connection, new PlayerConnection { PlayerId = 2, MatchSessionId = 100, JoinGeneration = 8 });
        var unit = em.CreateEntity(typeof(GhostInstance), typeof(GhostOwner), typeof(EntityOwner));
        em.SetComponentData(unit, new EntityOwner { PlayerID = 2 });
        var context = em.CreateEntity(typeof(GhostInstance), typeof(GhostOwner), typeof(PlayerContext), typeof(PlayerContextSession));
        em.SetComponentData(context, new PlayerContext { PlayerId = 2 });
        var system = world.GetOrCreateSystemManaged<PlayerGhostOwnershipSystem>();
        system.Update();
        Assert(em.GetComponentData<GhostOwner>(unit).NetworkId == 17, "Ownership must use connection NetworkId");
        Assert(em.GetComponentData<GhostOwner>(context).NetworkId == 17, "Context must use the same mapping");
        Assert(em.GetComponentData<PlayerContextSession>(context).JoinGeneration == 8, "Context must carry the current join");
        em.AddComponent<NetworkStreamRequestDisconnect>(connection);
        system.Update();
        Assert(em.GetComponentData<GhostOwner>(unit).NetworkId == 0, "Disconnected connection must lose ownership");
    }

    static void CheckBootstrap()
    {
        using var world = new World("Bootstrap checks", WorldFlags.GameServer);
        var em = world.EntityManager;
        var playerPrefab = em.CreateEntity(typeof(Prefab), typeof(PlayerContext), typeof(PlayerContextCache),
            typeof(PlayerContextCachePendingTag), typeof(ResourcePair), typeof(ResourcePairCache), typeof(PlayerTechnology), typeof(PlayerPendingTech));
        var matchPrefab = em.CreateEntity(typeof(Prefab), typeof(MatchSession), typeof(MatchState), typeof(MatchTimeComponent), typeof(MatchPlayerRoster));
        var root = em.CreateEntity(typeof(NetworkGameplayPrefabs), typeof(PlayerBootstrapSlot));
        em.SetComponentData(root, new NetworkGameplayPrefabs { PlayerContext = playerPrefab, Match = matchPrefab });
        var seed = em.CreateEntity(typeof(PlayerContextSeed), typeof(PlayerResourceSeed));
        em.SetComponentData(seed, new PlayerContextSeed { Value = new PlayerContext { PlayerId = 2, CivID = new FixedString64Bytes("test") } });
        em.GetBuffer<PlayerResourceSeed>(seed).Add(new PlayerResourceSeed { Value = new ResourcePair(ResourceType.Gold, 50) });
        em.GetBuffer<PlayerBootstrapSlot>(root).Add(new PlayerBootstrapSlot { PlayerId = 2, Context = seed });
        var system = world.GetOrCreateSystemManaged<NetworkGameplayBootstrapSystem>();
        system.Update(); system.Update();
        using var contexts = em.CreateEntityQuery(typeof(PlayerContext));
        using var matches = em.CreateEntityQuery(typeof(MatchSession), typeof(MatchState), typeof(MatchTimeComponent));
        Assert(contexts.CalculateEntityCount() == 1, "Seeds must materialize one context, not duplicate on subsequent frames");
        Assert(matches.CalculateEntityCount() == 1, "State/time must share exactly one match instance");
        Assert(matches.GetSingleton<MatchSession>().Id != 0, "Match session must be assigned");
        var context = contexts.GetSingletonEntity();
        Assert(em.GetBuffer<ResourcePair>(context)[0].Amount == 50, "Starting resources must survive seed conversion");
        Assert(em.GetBuffer<PlayerBootstrapSlot>(root)[0].Context == context, "Roster must point at runtime context");
    }

    static void CheckOfflineContext()
    {
        using var world = new World("Standalone offline context", WorldFlags.Game);
        var em = world.EntityManager;
        var seed = em.CreateEntity(typeof(PlayerContextSeed), typeof(PlayerResourceSeed));
        em.SetComponentData(seed, new PlayerContextSeed { Value = new PlayerContext { PlayerId = 9 } });
        world.GetOrCreateSystemManaged<NetworkGameplayBootstrapSystem>().Update();
        using var contexts = em.CreateEntityQuery(typeof(PlayerContext));
        Assert(contexts.CalculateEntityCount() == 1 && contexts.GetSingleton<PlayerContext>().PlayerId == 9,
            "Offline standalone context must not require network prefabs or bootstrap roster");
    }

    static void CheckHandshakeOrder()
    {
        using var world = new World("Handshake ordering", WorldFlags.GameClient);
        var em = world.EntityManager;
        var connection = em.CreateEntity(typeof(NetworkId), typeof(ClientPlayerState), typeof(RequiredBootstrapGhost));
        em.SetComponentData(connection, new NetworkId { Value = 17 });
        em.SetComponentData(connection, new ClientPlayerState { PlayerId = 2, MatchSessionId = 100, JoinGeneration = 3, Epoch = 1, ExpectedGhosts = -1 });
        var entry = em.CreateEntity(typeof(PlayerBootstrapGhostRpc), typeof(ReceiveRpcCommandRequest));
        em.SetComponentData(entry, new PlayerBootstrapGhostRpc { MatchSessionId = 100, JoinGeneration = 3, Epoch = 2, GhostId = 10, SpawnTick = new NetworkTick(1) });
        em.SetComponentData(entry, new ReceiveRpcCommandRequest { SourceConnection = connection });
        var summary = em.CreateEntity(typeof(PlayerBootstrapSummaryRpc), typeof(ReceiveRpcCommandRequest));
        em.SetComponentData(summary, new PlayerBootstrapSummaryRpc { MatchSessionId = 100, JoinGeneration = 3, Epoch = 2, Count = 1 });
        em.SetComponentData(summary, new ReceiveRpcCommandRequest { SourceConnection = connection });
        var system = world.GetOrCreateSystemManaged<PlayerHandshakeReceiveSystem>();
        system.Update();
        Assert(em.Exists(entry) && em.Exists(summary), "Future manifest must wait for its identity, not be discarded");
        var join = em.CreateEntity(typeof(PlayerJoinAcceptedRpc), typeof(ReceiveRpcCommandRequest));
        em.SetComponentData(join, new PlayerJoinAcceptedRpc { PlayerId = 2, MatchSessionId = 100, JoinGeneration = 3, Epoch = 2 });
        em.SetComponentData(join, new ReceiveRpcCommandRequest { SourceConnection = connection });
        system.Update();
        Assert(em.GetBuffer<RequiredBootstrapGhost>(connection).Length == 1
            && em.GetComponentData<ClientPlayerState>(connection).ExpectedGhosts == 1, "New identity must consume the deferred manifest");
    }

    static void CheckCommands()
    {
        using var world = new World("Command checks", WorldFlags.GameServer);
        var em = world.EntityManager;
        var match = em.CreateEntity(typeof(MatchState)); em.SetComponentData(match, new MatchState { Phase = MatchPhase.Running });
        var terrain = em.CreateEntity(); em.AddComponentObject(terrain, new TerrainVisualSource { Epoch = 5 });
        var queue = em.CreateEntity(typeof(CommandQueueComponent), typeof(CommandQueueElement));
        var connection = em.CreateEntity(typeof(NetworkId), typeof(PlayerConnection), typeof(PlayerReady));
        em.SetComponentData(connection, new NetworkId { Value = 17 });
        em.SetComponentData(connection, new PlayerConnection { PlayerId = 2, MatchSessionId = 100, JoinGeneration = 3, Epoch = 5 });
        em.SetComponentData(connection, new PlayerReady { MatchSessionId = 100, JoinGeneration = 3, Epoch = 5 });
        var unit = em.CreateEntity(typeof(GhostInstance), typeof(EntityOwner), typeof(MoveOverride), typeof(EntityHealth));
        var tick = new NetworkTick(1);
        em.SetComponentData(unit, new GhostInstance { ghostId = 10, spawnTick = tick });
        em.SetComponentData(unit, new EntityOwner { PlayerID = 2 });
        em.SetComponentData(unit, new EntityHealth { CurrentHP = 10, MaxHP = 10 });
        var system = world.GetOrCreateSystemManaged<GameplayCommandServerSystem>();
        uint requestSequence = 0;
        var command = new GameplayCommandRpc { MatchSessionId = 100, JoinGeneration = 3, Epoch = 5,
            SourceId = 10, SourceTick = tick, Type = CommandType.Move, Position = new float3(1, 0, 1) };
        Submit(command); system.Update();
        Assert(em.GetBuffer<CommandQueueElement>(queue).Length == 1, "Valid move must reach existing queue");
        Assert(em.GetBuffer<CommandQueueElement>(queue)[0].PlayerId == 2, "Authority must come from SourceConnection");
        em.GetBuffer<CommandQueueElement>(queue).Clear();
        command.JoinGeneration = 2; Submit(command); system.Update();
        Assert(em.GetBuffer<CommandQueueElement>(queue).Length == 0, "Previous join must not issue commands");
        command.JoinGeneration = 3; em.SetComponentData(unit, new EntityOwner { PlayerID = 5 });
        Submit(command); system.Update();
        Assert(em.GetBuffer<CommandQueueElement>(queue).Length == 0, "Other player's unit must not be controlled");
        em.SetComponentData(unit, new EntityOwner { PlayerID = 2 });
        var ge = em.CreateEntity(typeof(GridComponent));
        em.SetComponentData(ge, new GridComponent { initialized = true, width = 4, height = 4, cellsize = 1 });
        var fog = new FogOfWarSpatialSystemData
        {
            Ready = true, SourceGrid = ge,
            Visible = new NativeParallelMultiHashMap<int, int>(4, Allocator.Temp),
            Explored = new NativeParallelMultiHashMap<int, int>(4, Allocator.Temp)
        };
        try
        {
            var fe = em.CreateEntity(typeof(FogOfWarSpatialSystemData));
            em.SetComponentData(fe, fog);
            var resource = em.CreateEntity(typeof(GhostInstance), typeof(ResourceNodeData), typeof(LocalTransform));
            em.SetComponentData(resource, new GhostInstance { ghostId = 20, spawnTick = tick });
            em.SetComponentData(resource, LocalTransform.FromPosition(new float3(1.5f, 0, 1.5f)));
            fog.Visible.Add(5, 2);
            command.Type = CommandType.TargetTo; command.TargetId = 20; command.TargetTick = tick;
            Submit(command); system.Update();
            Assert(em.GetBuffer<CommandQueueElement>(queue).Length == 1
                && em.GetBuffer<CommandQueueElement>(queue)[0].targetEntity == resource,
                "Visible target must resolve to the server entity before entering the queue");
            em.GetBuffer<CommandQueueElement>(queue).Clear();
            fog.Visible.Clear(); Submit(command); system.Update();
            Assert(em.GetBuffer<CommandQueueElement>(queue).Length == 0, "Hidden target must be rejected by the server");
        }
        finally { fog.Visible.Dispose(); fog.Explored.Dispose(); }
        void Submit(GameplayCommandRpc value)
        {
            value.RequestId = ++requestSequence;
            var request = em.CreateEntity(typeof(GameplayCommandRpc), typeof(ReceiveRpcCommandRequest));
            em.SetComponentData(request, value);
            em.SetComponentData(request, new ReceiveRpcCommandRequest { SourceConnection = connection });
        }
    }
}
