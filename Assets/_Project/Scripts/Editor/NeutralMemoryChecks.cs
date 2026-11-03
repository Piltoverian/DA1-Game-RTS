using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public static class NeutralMemoryChecks
{
    static int checks;
    [MenuItem("Tools/RTS/Check Neutral Memory")]
    public static void Run()
    {
        checks = 0;
        CheckFrozenObservations(); CheckReplicaOrdering(); CheckGameplayIdentity(); CheckServerAndRejoin(); CheckReceiverOrdering();
        Debug.Log($"Neutral memory checks passed: {checks} assertions.");
    }
    static void Assert(bool value, string message) { checks++; if (!value) throw new InvalidOperationException(message); }

    static void CheckReceiverOrdering()
    {
        using var world = new World("Neutral pages before header", WorldFlags.GameClient);
        var em = world.EntityManager;
        var connection = em.CreateEntity(typeof(NetworkId), typeof(ClientPlayerState));
        em.SetComponentData(connection, new ClientPlayerState { MatchSessionId = 100, JoinGeneration = 3, Epoch = 7 });
        var receiver = world.GetOrCreateSystemManaged<NeutralMemoryClientSystem>();
        var entry = NeutralMemoryEntryRpc.From(Resource(11, 50, new int2(1)));
        entry.MatchSessionId = 100; entry.JoinGeneration = 3; entry.Epoch = 7;
        entry.StreamGeneration = 1; entry.Revision = 1; entry.SnapshotIndex = 0;
        var page = Receive(entry);
        var done = Receive(new NeutralMemoryDoneRpc { MatchSessionId = 100, JoinGeneration = 3, Epoch = 7,
            StreamGeneration = 1, CutoffRevision = 1, Count = 1 });
        receiver.Update();
        Assert(em.Exists(page) && em.Exists(done) && !receiver.Memory.Ready,
            "Pages and completion arriving before the header must remain pending");
        Receive(new NeutralMemoryHeaderRpc { MatchSessionId = 100, JoinGeneration = 3, Epoch = 7,
            StreamGeneration = 1, CutoffRevision = 1, Count = 1 });
        receiver.Update();
        Assert(!em.Exists(page) && !em.Exists(done) && receiver.Memory.Ready
            && receiver.Memory.Records[11].Data.Resource.Value.Amount == 50,
            "The later header must drain the retained page and completion and release Ready");

        Entity Receive<T>(T value) where T : unmanaged, IRpcCommand
        {
            var request = em.CreateEntity(typeof(T), typeof(ReceiveRpcCommandRequest));
            em.SetComponentData(request, value);
            em.SetComponentData(request, new ReceiveRpcCommandRequest { SourceConnection = connection });
            return request;
        }
    }

    static FogOfWarFrame Frame(params int[] cells)
    {
        var frame = new FogOfWarFrame { IsValid = true, PlayerId = 0, Width = 8, Height = 8, CellSize = 1 };
        foreach (int cell in cells) frame.VisibleCells.Add(cell);
        return frame;
    }
    static NeutralMemoryRecord Resource(ulong id, int amount, int2 cell) => new NeutralMemoryRecord {
        Id = id, TemplateGhostType = 2, ResourceType = ResourceType.Gold,
        Data = new FogEntityRenderData { OwnerId = -1, Kind = FogRenderKind.Resource,
            Transform = LocalTransform.FromPosition(new float3(cell.x + .5f, 0, cell.y + .5f)),
            PostTransform = float4x4.identity, FootprintMin = cell, FootprintMax = cell,
            Resource = new FogResourceSnapshot { Amount = amount }, IsStatic = true }
    };

    static void CheckFrozenObservations()
    {
        var first = new NeutralMemoryStore(); var second = new NeutralMemoryStore(); var unseen = new NeutralMemoryStore();
        using var visible = Frame(9); using var hidden = Frame();
        var resource = Resource(11, 100, new int2(1, 1));
        resource.Data.Entity = new Entity { Index = 123, Version = 9 }; resource.Data.SourceEntity = resource.Data.Entity;
        resource.Data.ActiveJob = new FogActiveJobSnapshot { Id = "private-production", RemainingWork = 20 };
        first.Observe(visible, new[] { resource });
        resource.Data.Resource = new FogResourceSnapshot { Amount = 45 };
        second.Observe(visible, new[] { resource });
        first.Observe(hidden, Array.Empty<NeutralMemoryRecord>()); unseen.Observe(hidden, Array.Empty<NeutralMemoryRecord>());
        Assert(first.Records[11].Data.Resource.Value.Amount == 100 && second.Records[11].Data.Resource.Value.Amount == 45,
            "Players must retain their own last observed stock, not the current live stock");
        Assert(unseen.Records.Count == 0, "Never observed neutrals must not appear in memory");
        Assert(first.Records[11].Data.Entity == Entity.Null && !first.Records[11].Data.SourceEntity.HasValue,
            "Memory must not retain raw simulation Entity references");
        Assert(!first.Records[11].Data.ActiveJob.HasValue && !NeutralMemoryEntryRpc.From(resource).ToRecord().Data.ActiveJob.HasValue,
            "Owner-only production details must never be widened into neutral memory");
        ulong revision = first.Revision;
        first.Observe(hidden, Array.Empty<NeutralMemoryRecord>());
        Assert(!first.Records[11].Removed && first.Revision == revision, "Hidden death/disappearance must preserve the record and revision");
        visible.IsValid = false; first.Observe(visible, Array.Empty<NeutralMemoryRecord>());
        Assert(!first.Records[11].Removed, "A transient invalid frame must never erase memory");
        var grid = new GridComponent { initialized = true, width = 8, height = 8, cellsize = 1 };
        Assert(first.TryKnownTarget(11, grid, out var known) && known.Position.x == 1.5f && known.ResourceType == ResourceType.Gold,
            "A known resource order must use frozen public fields");
        Assert(!first.TryKnownTarget(99, grid, out _) && !unseen.TryKnownTarget(11, grid, out _), "Unknown/private IDs must reject orders");
        visible.IsValid = true; first.Observe(visible, Array.Empty<NeutralMemoryRecord>());
        Assert(first.Records[11].Removed && !first.TryKnownTarget(11, grid, out _) && !second.Records[11].Removed,
            "Only the player's own server observation of an empty footprint may invalidate their memory");

        var moving = new NeutralMemoryStore(); var neutralUnit = Resource(12, 1, new int2(1, 1));
        neutralUnit.Data.Kind = FogRenderKind.Unit; neutralUnit.Data.IsStatic = false; neutralUnit.Data.Resource = null;
        moving.Observe(visible, new[] { neutralUnit }); moving.Observe(hidden, Array.Empty<NeutralMemoryRecord>());
        Assert(!moving.Records[12].Removed, "A neutral unit must be remembered even though it is mobile");
        using var newArea = Frame(18);
        neutralUnit.Data.Transform = LocalTransform.FromPosition(new float3(2.5f, 0, 2.5f));
        neutralUnit.Data.FootprintMin = neutralUnit.Data.FootprintMax = new int2(2, 2);
        moving.Observe(newArea, new[] { neutralUnit });
        Assert(moving.Records.Count == 1 && moving.Records[12].Data.Transform.Position.x == 2.5f,
            "Seeing a moved neutral must replace the same stable record without a duplicate");
        neutralUnit.Data.OwnerId = 5;
        moving.Observe(hidden, Array.Empty<NeutralMemoryRecord>());
        Assert(!moving.Records[12].Removed && moving.Records[12].Data.OwnerId == -1, "A hidden ownership conversion must not leak");
        moving.Observe(newArea, new[] { neutralUnit });
        Assert(moving.Records[12].Removed, "A visible conversion must retire the remembered neutral");
        neutralUnit.Id = 13; moving.Observe(newArea, new[] { neutralUnit });
        Assert(!moving.Records.ContainsKey(13), "Enemy units must never create neutral memory");
        var local = new FogOfWarRenderView();
        visible.Entities.Add(new FogEntityRenderData { Entity = new Entity { Index = 1, Version = 1 }, OwnerId = -1,
            Kind = FogRenderKind.Unit, FootprintMin = new int2(1, 1), FootprintMax = new int2(1, 1) });
        local.Apply(visible); local.Apply(hidden);
        visible.IsValid = false; local.Apply(visible);
        Assert(local.Entities.Count == 1, "Local presentation must share neutral mobile memory and invalid-frame preservation");
        local.Clear();
    }

    static void CheckReplicaOrdering()
    {
        var replica = new NeutralMemoryReplica();
        var header = new NeutralMemoryHeaderRpc { MatchSessionId = 100, JoinGeneration = 3, Epoch = 7,
            StreamGeneration = 1, CutoffRevision = 2, Count = 2 };
        replica.Begin(header);
        var newer = Resource(1, 30, new int2(1)); newer.Revision = 4;
        var delta = Entry(newer, -1); replica.Apply(delta);
        var older = Resource(1, 80, new int2(1)); older.Revision = 1;
        replica.Apply(Entry(older, 0));
        Assert(replica.Records[1].Data.Resource.Value.Amount == 30, "A late snapshot page must not overwrite a newer visible delta");
        var removed = Resource(2, 40, new int2(2)); removed.Revision = 3; removed.Removed = true;
        replica.Apply(Entry(removed, -1));
        var done = new NeutralMemoryDoneRpc { MatchSessionId = 100, JoinGeneration = 3, Epoch = 7,
            StreamGeneration = 1, CutoffRevision = 2, Count = 2 };
        replica.End(done);
        Assert(!replica.Ready, "A done marker cannot substitute for missing snapshot pages");
        removed.Removed = false; removed.Revision = 2; replica.Apply(Entry(removed, 1));
        Assert(replica.Ready && replica.Records[2].Removed, "Snapshot completion must remain stable while newer tombstones win");
        replica.Begin(header); Assert(replica.Ready, "Duplicate headers must not restart a completed snapshot");
        header.StreamGeneration = 2; header.Count = 0; replica.Begin(header);
        replica.Apply(delta); Assert(replica.Records.Count == 0, "A retired stream must not populate a new snapshot");
        done.StreamGeneration = 2; done.Count = 0; replica.End(done);
        Assert(replica.Ready, "An empty private memory snapshot must complete");
        var mismatched = delta; mismatched.StreamGeneration = 2; mismatched.JoinGeneration = 2; replica.Apply(mismatched);
        Assert(replica.Records.Count == 0, "A stale join generation must not modify current memory");
        var roundTrip = NeutralMemoryEntryRpc.From(newer).ToRecord();
        Assert(roundTrip.Id == newer.Id && roundTrip.Data.Resource.Value.Amount == 30
            && roundTrip.Data.Entity == Entity.Null && !roundTrip.Data.SourceEntity.HasValue, "Wire records must round trip public values without entities");
        header.StreamGeneration = 3; header.Count = 1; header.CutoffRevision = 8; replica.Begin(header);
        var live = Resource(8, 25, new int2(1)); live.ObservedTick = new NetworkTick { SerializedData = 41 };
        replica.ObserveLive(live);
        var delayed = Resource(8, 60, new int2(1)); delayed.Revision = 8; delayed.ObservedTick = new NetworkTick { SerializedData = 21 };
        var delayedPage = Entry(delayed, 0); delayedPage.StreamGeneration = 3; replica.Apply(delayedPage);
        Assert(replica.Records[8].Data.Resource.Value.Amount == 25 && replica.Records[8].Revision == 8,
            "A late RPC page must not overwrite a newer authorized live ghost observation");
        delayed.Revision = 9; delayed.ObservedTick = new NetworkTick { SerializedData = 61 }; delayed.Data.Resource = new FogResourceSnapshot { Amount = 15 };
        var laterDelta = Entry(delayed, -1); laterDelta.StreamGeneration = 3; replica.Apply(laterDelta);
        Assert(replica.Records[8].Data.Resource.Value.Amount == 15, "A genuinely newer server observation must advance the live memory");

        NeutralMemoryEntryRpc Entry(NeutralMemoryRecord value, int index)
        {
            var entry = NeutralMemoryEntryRpc.From(value);
            entry.MatchSessionId = 100; entry.JoinGeneration = 3; entry.Epoch = 7; entry.StreamGeneration = 1; entry.SnapshotIndex = index;
            return entry;
        }
    }

    static void CheckGameplayIdentity()
    {
        var tick = new NetworkTick { SerializedData = 5 };
        var first = new GhostInstance { ghostId = 3, ghostType = 1, spawnTick = tick };
        var moved = first; moved.ghostType = 2;
        Assert(NeutralMemoryIdentity.FromGhost(first) == NeutralMemoryIdentity.FromGhost(moved), "A render-template/ownership change must not change identity");
        moved.spawnTick = new NetworkTick { SerializedData = 7 };
        Assert(NeutralMemoryIdentity.FromGhost(first) != NeutralMemoryIdentity.FromGhost(moved)
            && NeutralMemoryIdentity.FromGhost(default) == 0, "Recycled IDs need their spawn tick; unassigned IDs must defer");
        using var world = new World("Neutral memory gameplay filter checks"); var em = world.EntityManager;
        var unit = em.CreateEntity(typeof(UnitComponent));
        Assert(NeutralMemoryIdentity.IsNeutralGameplay(em, unit), "Ownerless gameplay units are neutral");
        em.AddComponentData(unit, new EntityOwner { PlayerID = -1 });
        Assert(NeutralMemoryIdentity.IsNeutralGameplay(em, unit), "Negative owner IDs are neutral");
        em.SetComponentData(unit, new EntityOwner { PlayerID = 2 });
        Assert(!NeutralMemoryIdentity.IsNeutralGameplay(em, unit), "Owned enemy units are not neutral");
        foreach (var type in new[] { ComponentType.ReadWrite<BuildingComponent>(), ComponentType.ReadWrite<ResourceNodeData>(), ComponentType.ReadWrite<BlockageData>() })
        {
            var entity = em.CreateEntity(type); Assert(NeutralMemoryIdentity.IsNeutralGameplay(em, entity), "All gameplay neutral kinds must qualify");
            em.AddComponent<Prefab>(entity); Assert(!NeutralMemoryIdentity.IsNeutralGameplay(em, entity), "Prefabs must never qualify");
        }
        var projectile = em.CreateEntity(typeof(BlockageData), typeof(Bullet));
        Assert(!NeutralMemoryIdentity.IsNeutralGameplay(em, projectile), "Projectiles must never create memories");
        var system = em.CreateEntity(typeof(TerrainChunkGhost));
        Assert(!NeutralMemoryIdentity.IsNeutralGameplay(em, system), "System ghosts must never create memories");
    }

    static void CheckServerAndRejoin()
    {
        using var world = new World("Neutral memory server checks", WorldFlags.GameServer);
        var em = world.EntityManager;
        var gridEntity = em.CreateEntity(typeof(GridComponent));
        em.SetComponentData(gridEntity, new GridComponent { initialized = true, width = 8, height = 8, cellsize = 1 });
        var fog = new FogOfWarSpatialSystemData { Ready = true, SourceGrid = gridEntity,
            Visible = new NativeParallelMultiHashMap<int, int>(256, Allocator.Persistent),
            Explored = new NativeParallelMultiHashMap<int, int>(256, Allocator.Persistent) };
        var units = new NativeParallelMultiHashMap<int, Entity>(256, Allocator.Persistent);
        var neutrals = new NativeParallelMultiHashMap<int, Entity>(256, Allocator.Persistent);
        try
        {
            var fogEntity = em.CreateEntity(typeof(FogOfWarSpatialSystemData)); em.SetComponentData(fogEntity, fog);
            var unitIndex = em.CreateEntity(typeof(UnitSpatialBucket)); em.SetComponentData(unitIndex, new UnitSpatialBucket { Bucket = units });
            var neutralIndex = em.CreateEntity(typeof(NeutralSpatialBucket)); em.SetComponentData(neutralIndex, new NeutralSpatialBucket { Bucket = neutrals });
            var match = em.CreateEntity(typeof(MatchSession)); em.SetComponentData(match, new MatchSession { Id = 100 });
            for (int player = 0; player < 2; player++) { var context = em.CreateEntity(typeof(PlayerContext)); em.SetComponentData(context, new PlayerContext { PlayerId = player }); }
            var system = world.GetOrCreateSystemManaged<NeutralMemoryServerSystem>();
            var resource = Spawn(10, typeof(ResourceNodeData));
            em.SetComponentData(resource, new ResourceNodeData { Amount = 100, Type = ResourceType.Gold });
            fog.Visible.Add(9, 0); system.Update();
            ulong id = NeutralMemoryIdentity.FromGhost(em.GetComponentData<GhostInstance>(resource));
            Assert(system.Players[0].Records[id].Data.Resource.Value.Amount == 100 && system.Players[1].Records.Count == 0,
                "The real collector must only remember each player's visible neutrals");
            fog.Visible.Clear(); fog.Visible.Add(9, 1);
            em.SetComponentData(resource, new ResourceNodeData { Amount = 40, Type = ResourceType.Gold }); system.Update();
            Assert(system.Players[0].Records[id].Data.Resource.Value.Amount == 100 && system.Players[1].Records[id].Data.Resource.Value.Amount == 40,
                "Hidden live stock must not refresh a different player's server record");
            fog.Visible.Clear(); em.DestroyEntity(resource); system.Update();
            Assert(!system.Players[0].Records[id].Removed && !system.Players[1].Records[id].Removed, "Server entity destruction in fog must preserve both memories");
            fog.Visible.Add(9, 0); fog.Ready = false; em.SetComponentData(fogEntity, fog); system.Update();
            Assert(!system.Players[0].Records[id].Removed, "An incomplete server FOW frame must preserve memory");
            fog.Ready = true; em.SetComponentData(fogEntity, fog); system.Update();
            Assert(system.Players[0].Records[id].Removed && !system.Players[1].Records[id].Removed, "Reobserving the empty footprint must issue a private tombstone");
            var mobile = Spawn(11, typeof(UnitComponent));
            em.AddComponentData(mobile, new EntityOwner { PlayerID = -1 }); system.Update();
            ulong mobileId = NeutralMemoryIdentity.FromGhost(em.GetComponentData<GhostInstance>(mobile));
            Assert(system.Players[0].Records.ContainsKey(mobileId), "The server must remember neutral units with a negative owner");
            fog.Visible.Clear(); em.SetComponentData(mobile, new EntityOwner { PlayerID = 5 }); system.Update();
            Assert(!system.Players[0].Records[mobileId].Removed, "Hidden conversion must retain the neutral snapshot");
            fog.Visible.Add(9, 0); system.Update();
            Assert(system.Players[0].Records[mobileId].Removed, "Visible conversion must invalidate the same stable ID");
            var flowing = new List<Entity>();
            for (int i = 0; i < 75; i++) { var node = Spawn(100 + i, typeof(ResourceNodeData)); em.SetComponentData(node, new ResourceNodeData { Amount = 500, Type = ResourceType.Gold }); flowing.Add(node); }
            system.Update();
            var rosterEntity = em.CreateEntity(typeof(PersistentPlayerSlot));
            Entity connection = Connect(1);
            var replica = new NeutralMemoryReplica();
            for (int page = 0; page < 6 && !replica.Ready; page++)
            {
                em.SetComponentData(flowing[0], new ResourceNodeData { Amount = 499 - page, Type = ResourceType.Gold });
                system.Update(); Drain(replica, connection);
            }
            Assert(replica.Ready && replica.Records.Count == system.Players[0].Records.Count,
                "A bounded frozen snapshot must finish despite continuously changing observations");
            ulong flowingId = NeutralMemoryIdentity.FromGhost(em.GetComponentData<GhostInstance>(flowing[0]));
            Assert(replica.Records[flowingId].Revision == system.Players[0].Records[flowingId].Revision,
                "Newer deltas must survive older pages in the actual paged stream");
            em.DestroyEntity(connection); fog.Visible.Clear(); system.Update();
            Assert(NeutralMemoryServerSystem.TryGetKnownTarget(world, 0, flowingId, out var known) && known.Position.x == 1.5f
                && !NeutralMemoryServerSystem.TryGetKnownTarget(world, 1, flowingId, out _)
                && !NeutralMemoryServerSystem.TryGetKnownTarget(world, 0, mobileId, out _),
                "Disconnect must preserve safe known-resource orders while rejecting another player's unknown ID and converted units");
            connection = Connect(2); var restored = new NeutralMemoryReplica();
            for (int page = 0; page < 6 && !restored.Ready; page++) { system.Update(); Drain(restored, connection); }
            Assert(restored.Ready && restored.Header.JoinGeneration == 2
                && restored.Records[flowingId].Data.Resource.Value.Amount == system.Players[0].Records[flowingId].Data.Resource.Value.Amount,
                "A rejoin must restore private last-observed memory without a currently visible live ghost");

            Entity Spawn(int ghostId, ComponentType type)
            {
                var entity = em.CreateEntity(typeof(GhostInstance), typeof(LocalTransform), type);
                em.SetComponentData(entity, new GhostInstance { ghostId = ghostId, ghostType = 2, spawnTick = new NetworkTick { SerializedData = 5 } });
                em.SetComponentData(entity, LocalTransform.FromPosition(new float3(1.5f, 0, 1.5f)));
                neutrals.Add(9, entity); return entity;
            }
            Entity Connect(uint generation)
            {
                var entity = em.CreateEntity(typeof(NetworkId), typeof(NetworkStreamInGame), typeof(PlayerConnection));
                em.SetComponentData(entity, new NetworkId { Value = (int)generation });
                em.SetComponentData(entity, new PlayerConnection { PlayerId = 0, MatchSessionId = 100, JoinGeneration = generation, Epoch = 7 });
                var roster = em.GetBuffer<PersistentPlayerSlot>(rosterEntity); roster.Clear();
                roster.Add(new PersistentPlayerSlot { PlayerId = 0, MatchSessionId = 100, JoinGeneration = generation, Connection = entity, Claimed = true });
                return entity;
            }
            void Drain(NeutralMemoryReplica receiver, Entity target)
            {
                int count = 0;
                using var headers = em.CreateEntityQuery(typeof(NeutralMemoryHeaderRpc), typeof(SendRpcCommandRequest));
                using var hs = headers.ToEntityArray(Allocator.Temp);
                foreach (var request in hs) { Assert(em.GetComponentData<SendRpcCommandRequest>(request).TargetConnection == target, "Snapshot headers must be private"); receiver.Begin(em.GetComponentData<NeutralMemoryHeaderRpc>(request)); em.DestroyEntity(request); count++; }
                using var entries = em.CreateEntityQuery(typeof(NeutralMemoryEntryRpc), typeof(SendRpcCommandRequest));
                using var es = entries.ToEntityArray(Allocator.Temp);
                foreach (var request in es) { Assert(em.GetComponentData<SendRpcCommandRequest>(request).TargetConnection == target, "Snapshot entries must be private"); receiver.Apply(em.GetComponentData<NeutralMemoryEntryRpc>(request)); em.DestroyEntity(request); count++; }
                using var done = em.CreateEntityQuery(typeof(NeutralMemoryDoneRpc), typeof(SendRpcCommandRequest));
                using var ds = done.ToEntityArray(Allocator.Temp);
                foreach (var request in ds) { receiver.End(em.GetComponentData<NeutralMemoryDoneRpc>(request)); em.DestroyEntity(request); count++; }
                Assert(count <= NeutralMemoryServerSystem.RpcBudgetPerFrame, "All header, page, delta and done RPCs must share the per-frame budget");
            }
        }
        finally { fog.Visible.Dispose(); fog.Explored.Dispose(); units.Dispose(); neutrals.Dispose(); }
    }
}
