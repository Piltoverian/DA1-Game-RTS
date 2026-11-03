using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

public enum NetworkBuildPhase : byte { Planning, Placing, Site, Failed }

// Server-only correlation. Never replicate construction intent or its member list.
public struct NetworkBuildOrder : IComponentData
{
    public Entity Connection, Prefab, PlacementRequest, Site;
    public int PlayerId;
    public uint RequestId, JoinGeneration;
    public float3 Position;
    public NetworkBuildPhase Phase;
    public double AdmissionUntil;
}

public struct NetworkBuildMember : IBufferElementData
{
    public Entity Worker;
    public int Index;
    public bool Routed;
}

public struct NetworkWorkerCommand : IComponentData
{
    public Entity Connection;
    public uint JoinGeneration, RequestId;
}

public static class NetworkBuildOrders
{
    public static bool IsOlder(uint value, uint previous) => unchecked((int)(value - previous)) < 0;

    public static bool Current(EntityManager em, Entity worker, Entity order)
    {
        if (!em.Exists(order) || !em.HasComponent<NetworkBuildOrder>(order) || !em.Exists(worker)) return false;
        var value = em.GetComponentData<NetworkBuildOrder>(order);
        if (!em.HasComponent<NetworkWorkerCommand>(worker)) return false;
        var last = em.GetComponentData<NetworkWorkerCommand>(worker);
        return last.Connection == value.Connection && last.JoinGeneration == value.JoinGeneration && last.RequestId == value.RequestId;
    }

    public static bool ValidWorker(EntityManager em, Entity worker, in NetworkBuildOrder order)
    {
        if (!CommandAuthority.CanControl(em, order.PlayerId, worker) || !em.HasComponent<BuilderComponent>(worker)
            || !em.HasComponent<MoveOverride>(worker) || !em.HasBuffer<BuildOffer>(worker)
            || !em.Exists(order.Prefab) || !em.HasComponent<BuildingComponent>(order.Prefab)
            || (em.HasComponent<EntityHealth>(worker) && em.GetComponentData<EntityHealth>(worker).CurrentHP <= 0)) return false;
        var id = em.GetComponentData<BuildingComponent>(order.Prefab).DefinitionID;
        foreach (var offer in em.GetBuffer<BuildOffer>(worker)) if (offer.DefinitionID == id) return true;
        return false;
    }

    public static bool CanContinue(EntityManager em, in NetworkBuildOrder order)
    {
        if (!em.Exists(order.Site) || !em.HasComponent<BuildingConstruction>(order.Site)
            || !em.HasComponent<BuildingComponent>(order.Site) || !em.HasComponent<EntityOwner>(order.Site)
            || em.GetComponentData<EntityOwner>(order.Site).PlayerID != order.PlayerId
            || (em.HasComponent<EntityHealth>(order.Site) && em.GetComponentData<EntityHealth>(order.Site).CurrentHP <= 0)) return false;
        var construction = em.GetComponentData<BuildingConstruction>(order.Site);
        return (construction.Phase == ConstructionPhase.Planned || construction.Phase == ConstructionPhase.UnderConstruction)
            && construction.CompletedWork < em.GetComponentData<BuildingComponent>(order.Site).WorkLoad;
    }

    public static bool HoldsPlan(EntityManager em, Entity worker, Entity order)
    {
        if (!em.Exists(worker) || !em.HasComponent<BuilderComponent>(worker)) return false;
        var builder = em.GetComponentData<BuilderComponent>(worker);
        if (em.IsComponentEnabled<BuilderComponent>(worker) && builder.NetworkBuildOrder == order
            && builder.PlannedPrefab != Entity.Null) return true;
        if (em.HasBuffer<BuilderQueueElement>(worker))
            foreach (var queued in em.GetBuffer<BuilderQueueElement>(worker))
                if (queued.NetworkBuildOrder == order) return true;
        return false;
    }

    public static void Route(EntityManager em, Entity entity, Entity worker)
    {
        if (!em.Exists(entity) || !em.HasComponent<NetworkBuildOrder>(entity)) return;
        var order = em.GetComponentData<NetworkBuildOrder>(entity);
        var members = em.GetBuffer<NetworkBuildMember>(entity);
        for (int i = 0; i < members.Length; i++)
        {
            var member = members[i];
            if (member.Worker != worker || member.Routed) continue;
            member.Routed = true; members[i] = member;
            if (!Current(em, worker, entity) || !ValidWorker(em, worker, order) || order.Phase == NetworkBuildPhase.Failed) return;
            if (order.Phase == NetworkBuildPhase.Site)
            {
                if (CanContinue(em, order) && EntityCapabilities.CanBuild(em, worker, order.Site))
                    CommandDataHelper.AddCommandToQueue(em, order.PlayerId, worker, CommandType.Build,
                        targetEntity: order.Site, buildOrder: entity);
            }
            else if (order.Phase == NetworkBuildPhase.Placing)
            {
                if (em.Exists(order.PlacementRequest) && em.HasBuffer<PlaceBuildingWorkerElement>(order.PlacementRequest))
                    em.GetBuffer<PlaceBuildingWorkerElement>(order.PlacementRequest).Add(new PlaceBuildingWorkerElement { WorkerEntity = worker });
            }
            else BuilderSystem.EnqueuePlan(em, worker, order.Prefab, order.Position, entity);
            return;
        }
    }

    public static void Resolve(EntityManager em, Entity entity, Entity site)
    {
        if (!em.Exists(entity) || !em.HasComponent<NetworkBuildOrder>(entity)) return;
        var order = em.GetComponentData<NetworkBuildOrder>(entity);
        order.Site = site; order.PlacementRequest = Entity.Null;
        order.Phase = site == Entity.Null ? NetworkBuildPhase.Failed : NetworkBuildPhase.Site;
        em.SetComponentData(entity, order);
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(GameplayCommandServerSystem))]
[UpdateBefore(typeof(BuildingPlacementSystem))]
public partial class NetworkBuildOrderSystem : SystemBase
{
    protected override void OnUpdate()
    {
        if (!SystemAPI.TryGetSingleton<MatchState>(out var match) || match.Phase != MatchPhase.Running) return;
        using var query = EntityManager.CreateEntityQuery(typeof(NetworkBuildOrder), typeof(NetworkBuildMember));
        using var entities = query.ToEntityArray(Allocator.Temp);
        foreach (var entity in entities)
        {
            var order = EntityManager.GetComponentData<NetworkBuildOrder>(entity);
            if (order.Phase == NetworkBuildPhase.Placing && !EntityManager.Exists(order.PlacementRequest))
            {
                NetworkBuildOrders.Resolve(EntityManager, entity, Entity.Null);
                continue;
            }
            if (order.Phase != NetworkBuildPhase.Planning && order.Phase != NetworkBuildPhase.Placing
                && SystemAPI.Time.ElapsedTime > order.AdmissionUntil + 30)
            {
                bool referenced = false;
                foreach (var member in EntityManager.GetBuffer<NetworkBuildMember>(entity))
                    referenced |= NetworkBuildOrders.Current(EntityManager, member.Worker, entity)
                        && NetworkBuildOrders.HoldsPlan(EntityManager, member.Worker, entity);
                // Expiring member admission must not drop an accepted worker's queued plan.
                if (!referenced) EntityManager.DestroyEntity(entity);
                continue;
            }
            if (order.Phase != NetworkBuildPhase.Planning) continue;
            using var workers = new NativeList<Entity>(Allocator.Temp);
            bool waitingForQueue = false;
            foreach (var member in EntityManager.GetBuffer<NetworkBuildMember>(entity))
            {
                waitingForQueue |= !member.Routed;
                if (member.Routed && NetworkBuildOrders.Current(EntityManager, member.Worker, entity)
                    && NetworkBuildOrders.HoldsPlan(EntityManager, member.Worker, entity)
                    && NetworkBuildOrders.ValidWorker(EntityManager, member.Worker, order)) workers.Add(member.Worker);
            }
            if (workers.Length == 0)
            {
                if (!waitingForQueue) NetworkBuildOrders.Resolve(EntityManager, entity, Entity.Null);
                continue;
            }
            if (!FogOrderRules.TryGrid(EntityManager, out _, out var grid)) continue;
            if (FogOrderRules.HasFog(EntityManager) && (!EntityManager.HasComponent<BlockageData>(order.Prefab)
                || !PlayerSpawnPlacement.TryFootprint(grid, order.Position,
                    EntityManager.GetComponentData<BlockageData>(order.Prefab).LocalRect, out var min, out var max)
                || !FogOrderRules.CellsVisible(EntityManager, order.PlayerId, min, max, true))) continue;
            var request = EntityManager.CreateEntity(typeof(PlaceBuildingRequest), typeof(PlaceBuildingWorkerElement));
            EntityManager.SetComponentData(request, new PlaceBuildingRequest
            { PlayerId = order.PlayerId, PrefabEntity = order.Prefab, Position = order.Position, BuildOrder = entity });
            var buffer = EntityManager.GetBuffer<PlaceBuildingWorkerElement>(request);
            foreach (var worker in workers) buffer.Add(new PlaceBuildingWorkerElement { WorkerEntity = worker });
            order.Phase = NetworkBuildPhase.Placing; order.PlacementRequest = request;
            EntityManager.SetComponentData(entity, order);
        }
    }
}
