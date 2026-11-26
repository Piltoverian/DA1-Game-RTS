using System;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

// Explicit updates exercise the same gameplay systems in local and authoritative worlds.
public static class GameplayParityChecks
{
    static int checks;
    static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new InvalidOperationException(message);
    }

    [MenuItem("Tools/RTS/Check Gameplay Parity")]
    public static void Run()
    {
        checks = 0;
        string loggedError = null;
        void Capture(string message, string trace, LogType type)
        { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) loggedError ??= message; }
        Application.logMessageReceived += Capture;
        try
        {
            foreach (var flags in new[] { WorldFlags.Game, WorldFlags.GameServer })
            {
                using var f = new Fixture(flags);
                Gather(f);
                Research(f);
                Combat(f);
            }
            }
        finally { Application.logMessageReceived -= Capture; }
        Check(loggedError == null, "Gameplay systems logged an error: " + loggedError);
        Debug.Log($"Gameplay parity checks passed: {checks} assertions (local and server).");
    }

    static void Gather(Fixture f)
    {
        var em = f.Em;
        var node = em.CreateEntity(typeof(LocalTransform), typeof(ResourceNodeData));
        em.SetComponentData(node, LocalTransform.FromPosition(2, 0, 2));
        em.SetComponentData(node, new ResourceNodeData { Type = ResourceType.Gold, Amount = 3 });
        var depot = em.CreateEntity(typeof(LocalTransform), typeof(EntityOwner), typeof(BuildingConstruction));
        em.SetComponentData(depot, LocalTransform.FromPosition(2, 0, 2));
        em.SetComponentData(depot, new BuildingConstruction { Phase = ConstructionPhase.Completed });
        em.AddBuffer<StorageResource>(depot).Add(new StorageResource { Type = ResourceType.Gold });
        var worker = em.CreateEntity(typeof(LocalTransform), typeof(EntityOwner), typeof(WorkerTag),
            typeof(WorkerGatherData), typeof(TargetCache), typeof(MoveOverride));
        em.SetComponentEnabled<MoveOverride>(worker, false);
        em.SetComponentData(worker, LocalTransform.FromPosition(2, 0, 2));
        em.SetComponentData(worker, new WorkerGatherData { State = WorkerGatherState.Gathering,
            TargetNode = node, Capacity = 10, GatherRate = 5, WorkRate = 1, StopDistanceSq = 1 });
        var system = f.World.GetOrCreateSystem<WorkerGatherSystem>();
        system.Update(f.World.Unmanaged);
        var gathered = em.GetComponentData<WorkerGatherData>(worker);
        Check(gathered.CarryAmount == 3 && em.GetComponentData<ResourceNodeData>(node).Amount == 0,
            "Gather must clamp to remaining resources, conserving amount");
        Check(gathered.State == WorkerGatherState.ReturningDepot, "Depletion must return the carried resources");
        var before = f.Gold;
        system.Update(f.World.Unmanaged);
        Check(f.Gold == before + 3 && em.GetComponentData<WorkerGatherData>(worker).CarryAmount == 0,
            "Deposit must credit the owner exactly once and empty carry");
        system.Update(f.World.Unmanaged);
        Check(f.Gold == before + 3, "Idle worker must not deposit twice");
        em.DestroyEntity(depot);
        em.SetComponentData(worker, new WorkerGatherData { State = WorkerGatherState.ReturningDepot,
            TargetDepot = depot, CarryAmount = 2, CurrentResourceType = ResourceType.Gold });
        system.Update(f.World.Unmanaged);
        Check(em.GetComponentData<WorkerGatherData>(worker).CarryAmount == 2 && f.Gold == before + 3,
            "Destroyed depot must preserve carry without inventing a deposit");
        em.DestroyEntity(worker);
        em.DestroyEntity(node);
    }

    static void Research(Fixture f)
    {
        var em = f.Em;
        using var rb = new BlobBuilder(Allocator.Temp);
        ref var research = ref rb.ConstructRoot<ResearchBlob>();
        research.Job = new JobBlob { ID = "parity-job", WorkLoad = 1 };
        research.Tech.ID = "parity-tech";
        rb.Allocate(ref research.Tech.Cost, 1)[0] = new ResourcePair(ResourceType.Gold, 10);
        using var job = rb.CreateBlobAssetReference<ResearchBlob>(Allocator.Persistent);
        using var cb = new BlobBuilder(Allocator.Temp);
        ref var civ = ref cb.ConstructRoot<CivBlob>();
        civ.ID = "parity-civ"; civ.TechTreeID = "parity-tree";
        var unlock = cb.Allocate(ref civ.UnitUnlocks, 1);
        unlock[0].UnitID = "parity-unit";
        cb.Allocate(ref unlock[0].Prerequisites, 1)[0] = "parity-tech";
        using var civBlob = cb.CreateBlobAssetReference<CivBlob>(Allocator.Persistent);
        using var tb = new BlobBuilder(Allocator.Temp);
        ref var tree = ref tb.ConstructRoot<TechTreeBlob>();
        tree.ID = "parity-tree";
        tb.Allocate(ref tree.Nodes, 1)[0].TechID = "parity-tech";
        using var treeBlob = tb.CreateBlobAssetReference<TechTreeBlob>(Allocator.Persistent);
        var registry = em.CreateEntity(typeof(GameDataRegistryComponent));
        var entries = em.AddBuffer<RegistryBlobElement>(registry);
        entries.Add(RegistryBlobElement.Create(job)); entries.Add(RegistryBlobElement.Create(civBlob));
        entries.Add(RegistryBlobElement.Create(treeBlob));
        entries.AsNativeArray().Sort();
        var producer = em.CreateEntity(typeof(ProductionData), typeof(LocalTransform), typeof(EntityOwner),
            typeof(EntityWork), typeof(EntityHealth));
        em.SetComponentData(producer, LocalTransform.Identity);
        em.SetComponentData(producer, new EntityHealth { CurrentHP = 100 });
        em.SetComponentData(producer, new EntityWork { Rate = 10, JobCapacity = 2 });
        var offer = new ProductionElement { JobID = "parity-job", Kind = ProductionKind.Research };
        em.AddBuffer<ProductionElement>(producer).Add(offer);
        em.AddBuffer<ProductionQueueElement>(producer); em.AddBuffer<ProductionPayment>(producer);
        f.Gold = 9;
        ProductionJobs.Enqueue(em, producer, offer.JobReference, 0);
        Check(em.GetBuffer<ProductionQueueElement>(producer).Length == 0 && f.Gold == 9,
            "Insufficient funds must reject research without partial payment");
        f.Gold = 100;
        ProductionJobs.Enqueue(em, producer, offer.JobReference, 1);
        Check(em.GetBuffer<ProductionQueueElement>(producer).Length == 0 && f.Gold == 100,
            "Another player must not enqueue research on this producer");
        Check(TechUnlockHelper.CheckUnitUnlockStatus(em, f.Player, "parity-unit") == UnitUnlockStatus.Locked,
            "Unit must be locked before prerequisite research");
        ProductionJobs.Enqueue(em, producer, offer.JobReference, 0);
        Check(f.Gold == 90 && em.GetBuffer<PlayerPendingTech>(f.Player).Length == 1,
            "Research admission must charge once and record pending tech");
        ProductionJobs.Enqueue(em, producer, offer.JobReference, 0);
        Check(f.Gold == 90 && em.GetBuffer<ProductionQueueElement>(producer).Length == 1,
            "Pending duplicate must not charge or enqueue again");
        var production = f.World.GetOrCreateSystem<ProductionSystem>();
        production.Update(f.World.Unmanaged);
        Check(TechUnlockHelper.IsTechCompleted(em, f.Player, "parity-tech") &&
            TechUnlockHelper.CheckUnitUnlockStatus(em, f.Player, "parity-unit") == UnitUnlockStatus.Available,
            "Production completion must publish tech and unlock its dependent unit");
        Check(em.GetBuffer<ProductionQueueElement>(producer).Length == 0 &&
            em.GetBuffer<ProductionPayment>(producer).Length == 0 && em.GetBuffer<PlayerPendingTech>(f.Player).Length == 0,
            "Completion must clear queue, ledger and pending state");
        production.Update(f.World.Unmanaged);
        ProductionJobs.Enqueue(em, producer, offer.JobReference, 0);
        Check(f.Gold == 90 && em.GetBuffer<PlayerTechnology>(f.Player).Length == 1,
            "Completed tech must neither repeat nor refund its payment");
        em.DestroyEntity(producer); em.DestroyEntity(registry);
    }

    static void Combat(Fixture f)
    {
        var em = f.Em;
        var victim = em.CreateEntity(typeof(LocalTransform), typeof(EntityHealth), typeof(EntityOwner));
        em.SetComponentData(victim, LocalTransform.FromPosition(2, 0, 2));
        em.SetComponentData(victim, new EntityOwner { PlayerID = 1 });
        em.SetComponentData(victim, new EntityHealth { CurrentHP = 10 });
        Check(!CommandAuthority.CanControl(em, 0, victim), "Enemy unit must reject issuer control");
        var end = f.World.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();
        var bullet = em.CreateEntity(typeof(LocalTransform), typeof(Bullet), typeof(Target));
        em.SetComponentData(bullet, LocalTransform.FromPosition(2, 0, 2));
        em.SetComponentData(bullet, new Bullet { playerID = 0, damage = 10, speed = 1 });
        em.SetComponentData(bullet, new Target { targetEntity = victim });
        var mover = f.World.GetOrCreateSystem(typeof(ShootAttackSystem).Assembly.GetType("BulletMoverSystem"));
        mover.Update(f.World.Unmanaged); f.World.EntityManager.CompleteAllTrackedJobs(); end.Update();
        Check(em.GetComponentData<EntityHealth>(victim).CurrentHP == 0 && !em.Exists(bullet),
            "Projectile hit must apply lethal damage once and consume the projectile");
        var cleanup = f.World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        f.World.GetOrCreateSystem<EntityDeathSystem>().Update(f.World.Unmanaged); cleanup.Update();
        Check(!em.Exists(victim), "Death system must destroy the killed entity");
    }

    sealed class Fixture : IDisposable
    {
        public readonly World World;
        public EntityManager Em => World.EntityManager;
        public readonly Entity Player;
        NativeParallelMultiHashMap<int, Entity> bucket;
        public float Gold { get => Em.GetBuffer<ResourcePair>(Player)[0].Amount;
            set { var resources = Em.GetBuffer<ResourcePair>(Player); resources[0] = new ResourcePair(ResourceType.Gold, value); } }
        public Fixture(WorldFlags flags)
        {
            World = new World("Gameplay parity " + flags, flags);
            World.SetTime(new TimeData(1, 1));
            Em.CreateSingleton(new PlayerBootstrapState { Phase = PlayerBootstrapPhase.Ready });
            Em.CreateSingleton(new MatchState { Phase = MatchPhase.Running });
            World.GetOrCreateSystem<GameplayParityMatchGate>().Update(World.Unmanaged);
            var grid = Em.CreateSingleton(new GridComponent { width = 8, height = 8, cellsize = 1, initialized = true });
            var islands = Em.AddBuffer<GridIsland>(grid);
            for (int i = 0; i < 64; i++) islands.Add(new GridIsland { islandID = 1 });
            bucket = new NativeParallelMultiHashMap<int, Entity>(1, Allocator.Persistent);
            Em.CreateSingleton(new UnitSpatialBucket { Bucket = bucket });
            Player = Em.CreateSingleton(new PlayerContext { PlayerId = 0, TeamID = -1, CivID = "parity-civ" });
            Em.AddBuffer<ResourcePair>(Player).Add(new ResourcePair(ResourceType.Gold, 100));
            Em.AddBuffer<PlayerTechnology>(Player); Em.AddBuffer<PlayerPendingTech>(Player);
        }
        public void Dispose() { World.Dispose(); bucket.Dispose(); }
    }
}

[DisableAutoCreation]
public partial struct GameplayParityMatchGate : ISystem
{
    public void OnCreate(ref SystemState state) { ServerMatch.Initialize(ref state); }
    public void OnUpdate(ref SystemState state) { }
    public void OnDestroy(ref SystemState state) { ServerMatch.Shutdown(ref state); }
}
