using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public static class IncrementalBuildChecks
{
    [MenuItem("Tools/RTS/Check Incremental Building")]
    public static void Run()
    {
        using var world = new World("Incremental placement", WorldFlags.GameServer);
        var em = world.EntityManager;
        var match = em.CreateEntity(typeof(MatchState));
        em.SetComponentData(match, new MatchState { Phase = MatchPhase.Running });
        var terrain = em.CreateEntity(); em.AddComponentObject(terrain, new TerrainVisualSource { Epoch = 5 });
        var queue = em.CreateEntity(typeof(CommandQueueComponent), typeof(CommandQueueElement));
        var connection = em.CreateEntity(typeof(NetworkId), typeof(PlayerConnection), typeof(PlayerReady));
        em.SetComponentData(connection, new NetworkId { Value = 17 });
        em.SetComponentData(connection, new PlayerConnection { PlayerId = 2, MatchSessionId = 100, JoinGeneration = 3, Epoch = 5 });
        em.SetComponentData(connection, new PlayerReady { MatchSessionId = 100, JoinGeneration = 3, Epoch = 5 });
        var worker = em.CreateEntity(typeof(GhostInstance), typeof(EntityOwner), typeof(MoveOverride), typeof(EntityHealth),
            typeof(BuilderComponent), typeof(BuildOffer), typeof(LocalTransform), typeof(BuilderQueueElement));
        em.SetComponentData(worker, new GhostInstance { ghostId = 10, spawnTick = new NetworkTick(1) });
        em.SetComponentData(worker, new EntityOwner { PlayerID = 2 });
        em.SetComponentData(worker, new EntityHealth { CurrentHP = 10, MaxHP = 10 });
        em.GetBuffer<BuildOffer>(worker).Add(new BuildOffer { DefinitionID = "house" });
        var prefab = em.CreateEntity(typeof(Prefab), typeof(BuildingComponent), typeof(EntityOwner), typeof(EntityHealth),
            typeof(EntityResourceCost), typeof(BlockageData), typeof(LocalTransform));
        em.SetComponentData(prefab, new BuildingComponent { DefinitionID = "house", WorkLoad = 100 });
        using var builder = new BlobBuilder(Allocator.Temp);
        ref var definition = ref builder.ConstructRoot<BuildingBlob>();
        definition.Base.ID = "house"; definition.Base.PrefabIndex = 0;
        using var blob = builder.CreateBlobAssetReference<BuildingBlob>(Allocator.Persistent);
        var registry = em.CreateEntity(typeof(GameDataRegistryComponent), typeof(RegistryBlobElement), typeof(RegistryPrefabElement));
        em.GetBuffer<RegistryBlobElement>(registry).Add(RegistryBlobElement.Create(blob));
        em.GetBuffer<RegistryPrefabElement>(registry).Add(new RegistryPrefabElement { Prefab = prefab });
        var request = em.CreateEntity(typeof(GameplayCommandRpc), typeof(ReceiveRpcCommandRequest));
        em.SetComponentData(request, new GameplayCommandRpc { MatchSessionId = 100, JoinGeneration = 3, Epoch = 5,
            RequestId = 42, Type = CommandType.PlanBuild, SourceId = 10, SourceTick = new NetworkTick(1),
            DefinitionId = "house", Position = new float3(1, 0, 1), MemberCount = 3, MemberIndex = 0 });
        em.SetComponentData(request, new ReceiveRpcCommandRequest { SourceConnection = connection });
        var system = world.GetOrCreateSystemManaged<GameplayCommandServerSystem>();
        system.Update();
        if (em.GetBuffer<CommandQueueElement>(queue).Length != 1)
            throw new InvalidOperationException("First valid placement member must enqueue without waiting for the other members.");
        var orderEntity = em.GetBuffer<CommandQueueElement>(queue)[0].BuildOrder;
        Assert(orderEntity != Entity.Null, "First member must carry a shared order");
        em.GetBuffer<CommandQueueElement>(queue).Clear();
        Submit(42, worker, 0); system.Update();
        Assert(em.GetBuffer<CommandQueueElement>(queue).Length == 0, "Duplicate member must not enqueue or pay again");
        var second = MakeWorker(11);
        Submit(42, second, 1); system.Update();
        Assert(em.GetBuffer<CommandQueueElement>(queue).Length == 1
            && em.GetBuffer<CommandQueueElement>(queue)[0].BuildOrder == orderEntity, "Late member must use original order");
        em.GetBuffer<CommandQueueElement>(queue).Clear();
        var site = em.Instantiate(prefab);
        em.SetComponentData(site, new EntityOwner { PlayerID = 2 });
        em.SetComponentData(site, new EntityHealth { CurrentHP = 1, MaxHP = 100 });
        em.AddComponentData(site, new BuildingConstruction { Phase = ConstructionPhase.UnderConstruction });
        NetworkBuildOrders.Resolve(em, orderEntity, site);
        NetworkBuildOrders.Route(em, orderEntity, second);
        Assert(em.GetBuffer<CommandQueueElement>(queue).Length == 1
            && em.GetBuffer<CommandQueueElement>(queue)[0].Type == CommandType.Build
            && em.GetBuffer<CommandQueueElement>(queue)[0].targetEntity == site, "Late member must build the existing site");
        em.GetBuffer<CommandQueueElement>(queue).Clear();
        NetworkBuildOrders.Route(em, orderEntity, second);
        Assert(em.GetBuffer<CommandQueueElement>(queue).Length == 0, "Routed member must be idempotent");
        var third = MakeWorker(12);
        Submit(43, third, 0, CommandType.Move); system.Update();
        em.GetBuffer<CommandQueueElement>(queue).Clear();
        Submit(42, third, 2); system.Update();
        Assert(em.GetBuffer<CommandQueueElement>(queue).Length == 0, "Old late member must not override newer move");
        em.SetComponentData(site, new BuildingConstruction { Phase = ConstructionPhase.Completed, CompletedWork = 100 });
        var fourth = MakeWorker(13);
        Submit(42, fourth, 2); system.Update();
        Assert(em.GetBuffer<CommandQueueElement>(queue).Length == 0, "Completed site must not create another building");
        NetworkBuildOrders.Resolve(em, orderEntity, Entity.Null);
        Submit(42, fourth, 2); system.Update();
        Assert(em.GetBuffer<CommandQueueElement>(queue).Length == 0, "Failed placement must not restart on late member");
        // A client filling its four incomplete admissions cannot consume another connection's quota.
        for (uint id = 50; id < 54; id++) Submit(id, MakeWorker((int)id), 0);
        system.Update(); em.GetBuffer<CommandQueueElement>(queue).Clear();
        Submit(54, MakeWorker(54), 0); system.Update();
        Assert(em.GetBuffer<CommandQueueElement>(queue).Length == 0, "Incomplete groups must be bounded per connection");
        var other = em.CreateEntity(typeof(NetworkId), typeof(PlayerConnection), typeof(PlayerReady));
        em.SetComponentData(other, new NetworkId { Value = 18 });
        em.SetComponentData(other, em.GetComponentData<PlayerConnection>(connection));
        em.SetComponentData(other, em.GetComponentData<PlayerReady>(connection));
        Submit(55, MakeWorker(55), 0, CommandType.PlanBuild, other); system.Update();
        Assert(em.GetBuffer<CommandQueueElement>(queue).Length == 1, "Other connection must retain its own quota");
        em.GetBuffer<CommandQueueElement>(queue).Clear();
        // Active intent bounds must not depend on the bounded RPC dedup history.
        for (int i = 0; i < 64; i++)
        {
            var live = em.CreateEntity(typeof(NetworkBuildOrder));
            em.SetComponentData(live, new NetworkBuildOrder { Connection = other, Phase = NetworkBuildPhase.Planning });
        }
        Submit(56, MakeWorker(56), 0, CommandType.PlanBuild, other); system.Update();
        Assert(em.GetBuffer<CommandQueueElement>(queue).Length == 0, "Active ECS orders must remain bounded even outside ingress history");
        Debug.Log("Incremental building checks passed: immediate enqueue, late join, dedup, supersession, terminal state and quota isolation.");
        void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        Entity MakeWorker(int id)
        {
            var entity = em.Instantiate(worker);
            em.SetComponentData(entity, new GhostInstance { ghostId = id, spawnTick = new NetworkTick(1) });
            if (em.HasComponent<NetworkWorkerCommand>(entity)) em.RemoveComponent<NetworkWorkerCommand>(entity);
            return entity;
        }
        void Submit(uint id, Entity entity, int index, CommandType type = CommandType.PlanBuild, Entity issuer = default)
        {
            var rpc = em.CreateEntity(typeof(GameplayCommandRpc), typeof(ReceiveRpcCommandRequest));
            em.SetComponentData(rpc, new GameplayCommandRpc { MatchSessionId = 100, JoinGeneration = 3, Epoch = 5,
                RequestId = id, Type = type, SourceId = em.GetComponentData<GhostInstance>(entity).ghostId, SourceTick = new NetworkTick(1),
                DefinitionId = "house", Position = new float3(1, 0, 1), MemberCount = 3, MemberIndex = index });
            em.SetComponentData(rpc, new ReceiveRpcCommandRequest { SourceConnection = issuer == Entity.Null ? connection : issuer });
        }
    }
}
