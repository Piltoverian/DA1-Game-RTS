using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public static class NetworkBuildPlacementChecks
{
    static int checks;

    [MenuItem("Tools/RTS/Check Shared Build Placement")]
    public static void Run()
    {
        checks = 0;
        CheckSharedSite();
        CheckSupersededBeforeRouting();
        CheckAbandonedApproach();
        CheckQueuedPlanSurvivesAdmission();
        Debug.Log($"Shared build placement checks passed: {checks} assertions.");
    }

    static void CheckSharedSite()
    {
        using var f = new Fixture();
        var first = f.Worker();
        // Routing can remove a travel component: payload enumeration must tolerate that structural change.
        f.Em.AddComponent<FogTargetTravel>(first);
        var order = f.Start(first, true);
        Check(!f.Em.HasComponent<FogTargetTravel>(first), "New build must supersede prior fog travel without invalidating the queue payload");
        Check(f.Em.GetComponentData<BuilderComponent>(first).PlannedPrefab == f.Prefab,
            "First member must start its approach without waiting for remaining workers");
        f.Orders.Update();
        Check(f.Count<PlaceBuildingRequest>() == 0 && f.Gold == 1000, "Hidden footprint must not place or charge prematurely");

        f.Reveal();
        f.Orders.Update();
        Check(f.Count<PlaceBuildingRequest>() == 1, "Visible footprint must produce exactly one canonical placement request");
        var pendingWorker = f.Worker();
        f.Join(order, pendingWorker, 1);
        f.Queue.Update(f.World.Unmanaged);
        var placing = f.Em.GetComponentData<NetworkBuildOrder>(order);
        Check(f.Em.GetBuffer<PlaceBuildingWorkerElement>(placing.PlacementRequest).Length == 2,
            "Member arriving before placement resolves must join the existing request");
        f.Placement.Update(f.World.Unmanaged);
        var published = f.Em.GetComponentData<NetworkBuildOrder>(order);
        Check(published.Phase == NetworkBuildPhase.Site && f.Em.Exists(published.Site), "Placement must publish its canonical site before discarding the request");
        Check(f.Count<BuildingConstruction>() == 1 && f.Gold == 900, "One build order must instantiate and pay exactly once");
        f.Queue.Update(f.World.Unmanaged);
        f.Builders.Update(f.World.Unmanaged);
        Check(f.Em.GetComponentData<BuilderComponent>(first).TargetConstructionSite == published.Site,
            "Approaching first worker must resolve the shared plan to the published site");

        var late = f.Worker();
        f.Join(order, late, 2);
        f.Queue.Update(f.World.Unmanaged);
        f.Queue.Update(f.World.Unmanaged);
        Check(f.Em.GetComponentData<BuilderComponent>(late).TargetConstructionSite == published.Site,
            "Late member must route through Build to the same site");
        f.Queue.Update(f.World.Unmanaged);
        NetworkBuildOrders.Route(f.Em, order, late);
        f.Orders.Update();
        f.Placement.Update(f.World.Unmanaged);
        Check(f.Count<BuildingConstruction>() == 1 && f.Gold == 900 && f.Count<PlaceBuildingRequest>() == 0,
            "Duplicate routing must not create or charge another building");

        f.Em.SetComponentData(published.Site, new BuildingConstruction { Phase = ConstructionPhase.Completed, CompletedWork = 100 });
        var afterCompletion = f.Worker();
        f.Join(order, afterCompletion, 3);
        f.Queue.Update(f.World.Unmanaged);
        Check(f.CommandCount == 0 && f.Gold == 900, "Completed construction must not turn a late member into another placement or repair");
        f.Em.DestroyEntity(published.Site);
        var afterDestruction = f.Worker();
        f.Join(order, afterDestruction, 4);
        f.Queue.Update(f.World.Unmanaged);
        f.Orders.Update();
        f.Placement.Update(f.World.Unmanaged);
        Check(f.CommandCount == 0 && f.Count<BuildingConstruction>() == 0 && f.Gold == 900,
            "Destroyed site must not be silently recreated by a delayed member");
    }

    static void CheckSupersededBeforeRouting()
    {
        using var f = new Fixture();
        var worker = f.Worker();
        var order = f.Start(worker, false);
        var last = f.Em.GetComponentData<NetworkWorkerCommand>(worker);
        last.RequestId++;
        f.Em.SetComponentData(worker, last);
        f.Queue.Update(f.World.Unmanaged);
        f.Orders.Update();
        Check(f.Em.GetComponentData<NetworkBuildOrder>(order).Phase == NetworkBuildPhase.Failed,
            "A same-tick newer command must retire the unrouted old intent rather than leave it waiting forever");
        f.Reveal();
        f.Orders.Update();
        Check(f.Count<PlaceBuildingRequest>() == 0 && f.Gold == 1000, "Superseded old intent must never charge when its location becomes visible later");
    }

    static void CheckAbandonedApproach()
    {
        using var f = new Fixture();
        var worker = f.Worker();
        var order = f.Start(worker, true);
        f.Orders.Update();
        // This is the state published when an unreachable approach is abandoned.
        var builder = f.Em.GetComponentData<BuilderComponent>(worker);
        builder.PlannedPrefab = Entity.Null;
        f.Em.SetComponentData(worker, builder);
        f.Em.SetComponentEnabled<BuilderComponent>(worker, false);
        f.Orders.Update();
        Check(f.Em.GetComponentData<NetworkBuildOrder>(order).Phase == NetworkBuildPhase.Failed,
            "An abandoned approach must close its order despite retaining the same last network sequence");
        f.Reveal();
        f.Orders.Update();
        Check(f.Count<PlaceBuildingRequest>() == 0 && f.Gold == 1000, "Exploration by another unit must not resurrect an abandoned placement");
    }

    static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(message);
    }

    static void CheckQueuedPlanSurvivesAdmission()
    {
        using var f = new Fixture();
        var worker = f.Worker();
        var order = f.Start(worker, true);
        f.Reveal(); f.Orders.Update(); f.Placement.Update(f.World.Unmanaged);
        var site = f.Em.GetComponentData<NetworkBuildOrder>(order).Site;
        f.World.SetTime(new Unity.Core.TimeData(100, .05f));
        // The first worker still references the accepted plan while its Build command is queued.
        f.Orders.Update();
        Check(f.Em.Exists(order) && f.Em.Exists(site), "Admission expiry must not erase an accepted active/queued plan");
        f.Queue.Update(f.World.Unmanaged);
        f.Builders.Update(f.World.Unmanaged);
        f.Orders.Update();
        Check(f.Em.Exists(site) && f.Gold == 900, "Order bookkeeping cleanup must preserve the site and its single payment");
    }

    sealed class Fixture : IDisposable
    {
        public readonly World World = new("Shared build placement integration", WorldFlags.Game);
        public EntityManager Em => World.EntityManager;
        public readonly Entity Prefab;
        public readonly SystemHandle Queue, Placement, Builders;
        public readonly NetworkBuildOrderSystem Orders;
        readonly Entity connection, context, fogEntity;
        readonly float3 position = new(5.5f, 0, 5.5f);
        FogOfWarSpatialSystemData fog;
        uint nextRequest;

        public Fixture()
        {
            var grid = Em.CreateEntity(typeof(GridComponent), typeof(GridTerrain), typeof(GridNodeCost));
            Em.SetComponentData(grid, new GridComponent { width = 16, height = 16, cellsize = 1, initialized = true });
            var terrain = Em.GetBuffer<GridTerrain>(grid);
            var costs = Em.GetBuffer<GridNodeCost>(grid);
            for (int i = 0; i < 256; i++)
            {
                terrain.Add(new GridTerrain { walkable = true, RampId = -1 });
                costs.Add(new GridNodeCost { cost = 1 });
            }
            var match = Em.CreateEntity(typeof(MatchState));
            Em.SetComponentData(match, new MatchState { Phase = MatchPhase.Running });
            connection = Em.CreateEntity();
            context = Em.CreateEntity(typeof(PlayerContext), typeof(ResourcePair));
            Em.SetComponentData(context, new PlayerContext { PlayerId = 1 });
            Em.GetBuffer<ResourcePair>(context).Add(new ResourcePair(ResourceType.Gold, 1000));
            Prefab = Em.CreateEntity(typeof(Prefab), typeof(BuildingComponent), typeof(EntityOwner), typeof(EntityHealth),
                typeof(EntityResourceCost), typeof(BlockageData), typeof(LocalTransform), typeof(GridFootprint));
            Em.SetComponentData(Prefab, new BuildingComponent { DefinitionID = "shared-house", WorkLoad = 100 });
            Em.SetComponentData(Prefab, new EntityHealth { CurrentHP = 100, MaxHP = 100 });
            Em.SetComponentData(Prefab, LocalTransform.Identity);
            Em.SetComponentData(Prefab, new GridFootprint { Cells = 1, WorldSize = 1 });
            var rect = new StartEndRect(new float2(-.5f)); rect.ExpandTo(new float2(.5f));
            Em.SetComponentData(Prefab, new BlockageData { LocalRect = rect, CustomCost = 255 });
            Em.GetBuffer<EntityResourceCost>(Prefab).Add(new EntityResourceCost { Type = ResourceType.Gold, Amount = 100 });
            fog = new FogOfWarSpatialSystemData
            {
                Visible = new NativeParallelMultiHashMap<int, int>(16, Allocator.Persistent),
                Explored = new NativeParallelMultiHashMap<int, int>(16, Allocator.Persistent),
                Ready = true, SourceGrid = grid
            };
            fogEntity = Em.CreateEntity(typeof(FogOfWarSpatialSystemData)); Em.SetComponentData(fogEntity, fog);
            // CommandQueue remains internal to the runtime assembly; no production API is widened for this check.
            Queue = World.GetOrCreateSystem(typeof(CommandDataHelper).Assembly.GetType("CommandQueue", true));
            Placement = World.GetOrCreateSystem(typeof(BuildingPlacementSystem));
            Builders = World.GetOrCreateSystem(typeof(BuilderSystem));
            Orders = World.GetOrCreateSystemManaged<NetworkBuildOrderSystem>();
        }

        public float Gold => Em.GetBuffer<ResourcePair>(context)[0].Amount;
        public int CommandCount
        {
            get
            {
                using var query = Em.CreateEntityQuery(typeof(CommandQueueComponent));
                return Em.GetBuffer<CommandQueueElement>(query.GetSingletonEntity()).Length;
            }
        }

        public Entity Worker()
        {
            var worker = Em.CreateEntity(typeof(EntityOwner), typeof(BuilderComponent), typeof(BuilderQueueElement),
                typeof(MoveOverride), typeof(EntityHealth), typeof(LocalTransform), typeof(BuildOffer), typeof(TargetCache));
            Em.SetComponentData(worker, new EntityOwner { PlayerID = 1 });
            Em.SetComponentData(worker, new EntityHealth { CurrentHP = 10, MaxHP = 10 });
            Em.SetComponentData(worker, LocalTransform.FromPosition(new float3(2.5f, 0, 2.5f)));
            Em.SetComponentData(worker, new BuilderComponent { BuildRange = 1, BuildWorkLoadPerSecond = 1 });
            Em.SetComponentEnabled<BuilderComponent>(worker, false);
            Em.GetBuffer<BuildOffer>(worker).Add(new BuildOffer { DefinitionID = "shared-house" });
            return worker;
        }

        public Entity Start(Entity worker, bool route)
        {
            var order = Em.CreateEntity(typeof(NetworkBuildOrder), typeof(NetworkBuildMember));
            Em.SetComponentData(order, new NetworkBuildOrder { Connection = connection, PlayerId = 1, JoinGeneration = 1,
                RequestId = ++nextRequest, Prefab = Prefab, Position = position, AdmissionUntil = 10 });
            Join(order, worker, 0);
            if (route) Queue.Update(World.Unmanaged);
            return order;
        }

        public void Join(Entity orderEntity, Entity worker, int index)
        {
            var order = Em.GetComponentData<NetworkBuildOrder>(orderEntity);
            Em.GetBuffer<NetworkBuildMember>(orderEntity).Add(new NetworkBuildMember { Worker = worker, Index = index });
            var sequence = new NetworkWorkerCommand { Connection = order.Connection, JoinGeneration = order.JoinGeneration, RequestId = order.RequestId };
            if (Em.HasComponent<NetworkWorkerCommand>(worker)) Em.SetComponentData(worker, sequence);
            else Em.AddComponentData(worker, sequence);
            CommandDataHelper.AddPlanBuildToQueue(Em, order.PlayerId, new[] { worker }, order.Prefab, order.Position, orderEntity);
        }

        public void Reveal()
        {
            if (!fog.IsVisibleTo(5 * 16 + 5, 1)) fog.Visible.Add(5 * 16 + 5, 1);
            Em.SetComponentData(fogEntity, fog);
        }

        public int Count<T>() where T : unmanaged, IComponentData
        {
            using var query = Em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.CalculateEntityCount();
        }

        public void Dispose()
        {
            World.Dispose();
            if (fog.Visible.IsCreated) fog.Visible.Dispose();
            if (fog.Explored.IsCreated) fog.Explored.Dispose();
        }
    }
}
