using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateBefore(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(TransformSystemGroup))]
[UpdateBefore(typeof(PopulationSystem))]
public partial class PlayerBootstrapSystem : SystemBase
{
    struct Placement { public Entity Prefab; public int Owner; public float3 Position; public bool Building; public bool Resource;
        public FixedString64Bytes ResourceId; public ResourceValueTier ResourceTier; public int ResourceRole, AmountPerMine; }

    protected override void OnCreate()
    {
        RequireForUpdate<PlayerBootstrapState>();
        RequireForUpdate<GridComponent>();
        RequireForUpdate<GameDataRegistryComponent>();
    }

    protected override void OnUpdate()
    {
        var em = EntityManager;
        var root = SystemAPI.GetSingletonEntity<PlayerBootstrapState>();
        var status = em.GetComponentData<PlayerBootstrapState>(root);
        if (status.Phase != PlayerBootstrapPhase.Waiting) return;
        var ge = SystemAPI.GetSingletonEntity<GridComponent>();
        var grid = em.GetComponentData<GridComponent>(ge);
        if (!em.HasBuffer<GridSpawnCell>(ge) || !em.HasBuffer<GridTerrain>(ge))
        { Fail(root, status, "Generated terrain and spawn slots are required."); return; }
        int size = grid.width * grid.height;
        if (em.GetBuffer<GridNodeCost>(ge).Length != size || em.GetBuffer<GridTerrain>(ge).Length != size ||
            em.GetBuffer<CostChangeRequest>(ge).Length != 0 || grid.isDirty || grid.islandGeneration != grid.generation) return;

        var re = SystemAPI.GetSingletonEntity<GameDataRegistryComponent>();
        using var slots = em.GetBuffer<PlayerBootstrapSlot>(root).ToNativeArray(Allocator.Temp);
        using var spawns = em.GetBuffer<GridSpawnCell>(ge).ToNativeArray(Allocator.Temp);
        using var resources = em.GetBuffer<PlayerBootstrapResource>(root).ToNativeArray(Allocator.Temp);
        using var terrain = em.GetBuffer<GridTerrain>(ge).ToNativeArray(Allocator.Temp);
        using var costs = em.GetBuffer<GridNodeCost>(ge).ToNativeArray(Allocator.Temp);
        using var occupiedStorage = new NativeArray<bool>(size, Allocator.Temp);
        var occupied = occupiedStorage;
        var plan = new List<Placement>();
        using var existingQuery = em.CreateEntityQuery(typeof(UnitComponent), typeof(LocalTransform));
        using (var existing = existingQuery.ToEntityArray(Allocator.Temp))
            foreach (var entity in existing)
            {
                var cell = GridHelper.WorldToGrid(em.GetComponentData<LocalTransform>(entity).Position, grid);
                if (PlayerSpawnPlacement.InBounds(cell, grid)) occupied[GridHelper.GetNodeIndex(cell, grid)] = true;
            }
        using var contextsQuery = em.CreateEntityQuery(typeof(PlayerContext));
        using var contexts = contextsQuery.ToEntityArray(Allocator.Temp);
        if (contexts.Length != slots.Length) { Fail(root, status, "Use the bootstrap roster for every player; remove standalone PlayerContextAuthoring instances."); return; }

        var prefabs = em.GetBuffer<RegistryPrefabElement>(re);
        var registry = em.GetBuffer<RegistryBlobElement>(re);
        var ids = new HashSet<int>();
        var assignedSlots = new HashSet<int>();
        foreach (var slot in slots)
        {
            if (!ids.Add(slot.PlayerId) || !assignedSlots.Add(slot.SpawnSlot) || !em.Exists(slot.Context) ||
                !em.HasComponent<PlayerContext>(slot.Context) || em.GetComponentData<PlayerContext>(slot.Context).PlayerId != slot.PlayerId)
            { Fail(root, status, "Invalid or duplicate player/slot mapping."); return; }
            int contextCount = 0;
            foreach (var entity in contexts)
                if (em.GetComponentData<PlayerContext>(entity).PlayerId == slot.PlayerId) contextCount++;
            if (contextCount != 1) { Fail(root, status, "Player IDs must be unique across all contexts."); return; }
            int2 anchor = default;
            int matches = 0;
            foreach (var spawn in spawns) if (spawn.playerId == slot.SpawnSlot) { anchor = spawn.cell; matches++; }
            if (matches != 1) { Fail(root, status, "Spawn slot does not resolve to exactly one terrain anchor."); return; }
            var civ = registry.GetBlobByID<CivBlob>(em.GetComponentData<PlayerContext>(slot.Context).CivID, out var lookup);
            if (lookup != FunctionResult.Success || !TryPrefab(prefabs, civ.Value.TownHallPrefab, out var hall) ||
                (status.WorkerCount > 0 && !TryPrefab(prefabs, civ.Value.StartWorkerPrefab, out _)))
            { Fail(root, status, "Civilization or starting prefab is missing from registry."); return; }
            if (!ValidPrefab(hall, true)) { Fail(root, status, "Town hall prefab requires building, completed-construction support, main-base tag and blocking footprint."); return; }
            if (!PlayerSpawnPlacement.TryBuilding(grid, terrain, costs, occupied, anchor, status.PlacementRadiusCells,
                em.GetComponentData<BlockageData>(hall).LocalRect, out var position, out var min, out var max,
                em.HasComponent<GridFootprint>(hall) ? em.GetComponentData<GridFootprint>(hall).Cells : 0))
            { Fail(root, status, "Not enough flat free space and exit ring for every starting building."); return; }
            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
                if (occupied[GridHelper.GetNodeIndex(new int2(x, y), grid)])
                { Fail(root, status, "Starting building overlaps an existing unit."); return; }
            PlayerSpawnPlacement.Reserve(grid, costs, min, max, 255);
            plan.Add(new Placement { Prefab = hall, Owner = slot.PlayerId, Position = position, Building = true });
        }
        // The same resource package is planned for every slot, before workers and before any entity exists.
        if (em.HasComponent<MapResourceSettings>(root))
        {
            var bases = new int2[slots.Length];
            for (int p = 0; p < slots.Length; p++)
                foreach (var spawn in spawns) if (spawn.playerId == slots[p].SpawnSlot) bases[p] = spawn.cell;
            List<MapResourcePlanner.Node> nodes;
            MapResourceReport report;
            try { nodes = MapResourcePlanner.Plan(em, grid, terrain, costs, occupied, bases,
                em.GetComponentData<MapResourceSettings>(root), em.GetBuffer<MapResourcePrefab>(root), out report); }
            catch (System.InvalidOperationException error) { Fail(root, status, error.Message); return; }
            em.SetComponentData(root, report);
            Debug.Log($"Map resources: {report.PlacedClusters}/{report.RequestedClusters} clusters, {report.Nodes} nodes; missing={report.RequestedClusters - report.PlacedClusters}.");
            foreach (var node in nodes)
                plan.Add(new Placement { Prefab = node.Prefab, Owner = -1, Position = node.Position, Resource = true,
                    ResourceId = node.DefinitionId, ResourceTier = node.Tier, ResourceRole = node.Role, AmountPerMine = node.AmountPerMine });
        }
        foreach (var slot in slots)
        foreach (var resource in resources)
        {
            var prefab = resource.Prefab;
            if (!em.Exists(prefab) || !em.HasComponent<ResourceNodeData>(prefab) || !em.HasComponent<ResourceNodeTag>(prefab) ||
                !em.HasComponent<BlockageData>(prefab) || !em.HasComponent<GridFootprint>(prefab) ||
                !em.HasComponent<LocalTransform>(prefab) || resource.AmountPerMine <= 0 ||
                em.GetComponentData<BlockageData>(prefab).CustomCost != 255 || em.GetComponentData<GridFootprint>(prefab).WorldSize <= 0)
            { Fail(root, status, "Starting resource requires a baked snapped prefab, positive amount and blocking footprint."); return; }
            int2 anchor = default;
            foreach (var spawn in spawns) if (spawn.playerId == slot.SpawnSlot) anchor = spawn.cell;
            for (int n = 0; n < resource.CountPerPlayer; n++)
            {
                if (!PlayerSpawnPlacement.TryBuilding(grid, terrain, costs, occupied, anchor, status.PlacementRadiusCells,
                    em.GetComponentData<BlockageData>(prefab).LocalRect, out var position, out var min, out var max,
                    em.GetComponentData<GridFootprint>(prefab).Cells, resource.MinDistanceCells))
                { Fail(root, status, "Not enough space for the complete shared starting resource package."); return; }
                PlayerSpawnPlacement.Reserve(grid, costs, min, max, 255);
                plan.Add(new Placement { Prefab = prefab, Owner = -1, Position = position, Resource = true, AmountPerMine = resource.AmountPerMine });
            }
        }
        // All buildings are reserved before any workers are placed.
        for (int p = 0; p < slots.Length; p++)
        {
            var slot = slots[p];
            var civ = registry.GetBlobByID<CivBlob>(em.GetComponentData<PlayerContext>(slot.Context).CivID, out _);
            if (status.WorkerCount == 0) continue;
            TryPrefab(prefabs, civ.Value.StartWorkerPrefab, out var worker);
            if (!ValidPrefab(worker, false)) { Fail(root, status, "Starting worker prefab requires unit, owner, health, selection, transform and movement components."); return; }
            bool unlockedWorker = false;
            var workerId = em.GetComponentData<UnitComponent>(worker).DefinitionID;
            for (int i = 0; i < civ.Value.UnitUnlocks.Length; i++)
                if (civ.Value.UnitUnlocks[i].UnitID == workerId && civ.Value.UnitUnlocks[i].Prerequisites.Length == 0)
                    unlockedWorker = true;
            if (!unlockedWorker) { Fail(root, status, "Starting worker must belong to the civilization roster with no tech prerequisites."); return; }
            var hall = plan[p];
            PlayerSpawnPlacement.TryFootprint(grid, hall.Position, em.GetComponentData<BlockageData>(hall.Prefab).LocalRect, out var min, out var max);
            int2 anchor = default;
            foreach (var spawn in spawns) if (spawn.playerId == slot.SpawnSlot) anchor = spawn.cell;
            for (int n = 0; n < status.WorkerCount; n++)
            {
                if (!PlayerSpawnPlacement.TryUnit(grid, costs, occupied, anchor, status.PlacementRadiusCells, min, max, out var cell))
                { Fail(root, status, "Not enough connected free cells for the full shared worker loadout."); return; }
                occupied[GridHelper.GetNodeIndex(cell, grid)] = true;
                plan.Add(new Placement { Prefab = worker, Owner = slot.PlayerId, Position = GridHelper.GridToWorld(cell, grid) });
            }
        }

        // No structural changes until the entire roster has a valid plan.
        foreach (var placement in plan)
        {
            Entity entity = em.Instantiate(placement.Prefab);
            var transform = em.GetComponentData<LocalTransform>(entity);
            var position = placement.Position;
            position.y = transform.Position.y; // Current gameplay terrain uses flat Y; preserve the prefab pivot.
            transform.Position = position;
            em.SetComponentData(entity, transform);
            if (placement.Resource)
            {
                var nodeData = em.GetComponentData<ResourceNodeData>(entity);
                nodeData.Amount = placement.AmountPerMine;
                em.SetComponentData(entity, nodeData);
                em.RemoveComponent<ResourceNodePendingConfig>(entity);
                if (!placement.ResourceId.IsEmpty)
                {
                    em.AddComponentData(entity, new MapResourceIdentity { DefinitionId = placement.ResourceId,
                        Tier = placement.ResourceTier, ClusterRole = placement.ResourceRole });
                }
                if (!em.HasComponent<BlockageNeedBakeTag>(entity)) em.AddComponent<BlockageNeedBakeTag>(entity);
                em.SetComponentEnabled<BlockageNeedBakeTag>(entity, true);
                em.GetBuffer<PlayerBootstrapSpawned>(root).Add(new PlayerBootstrapSpawned { Value = entity });
                continue;
            }
            em.SetComponentData(entity, new EntityOwner { PlayerID = placement.Owner });
            var selection = em.GetComponentData<Selectable>(entity);
            selection.playerID = placement.Owner;
            em.SetComponentData(entity, selection);
            var health = em.GetComponentData<EntityHealth>(entity);
            health.CurrentHP = health.MaxHP; health.Changed = true;
            em.SetComponentData(entity, health);
            if (placement.Building)
            {
                em.SetComponentData(entity, new BuildingConstruction { Phase = ConstructionPhase.Completed,
                    CompletedWork = em.GetComponentData<BuildingComponent>(entity).WorkLoad });
                if (!em.HasComponent<BlockageNeedBakeTag>(entity)) em.AddComponent<BlockageNeedBakeTag>(entity);
                em.SetComponentEnabled<BlockageNeedBakeTag>(entity, true);
            }
            else
            {
                if (!em.HasComponent<SetupUnitMoverDefaultPosition>(entity)) em.AddComponent<SetupUnitMoverDefaultPosition>(entity);
                var agent = em.GetComponentData<MovementAgentComponent>(entity);
                agent.hastarget = false;
                agent.velocity = float3.zero;
                agent.preferredVelocity = float3.zero;
                em.SetComponentData(entity, agent);
                if (em.HasComponent<MoveOverride>(entity)) em.SetComponentEnabled<MoveOverride>(entity, false);
            }
            em.GetBuffer<PlayerBootstrapSpawned>(root).Add(new PlayerBootstrapSpawned { Value = entity });
        }
        status.Phase = PlayerBootstrapPhase.Finalizing;
        status.GridGeneration = grid.generation;
        em.SetComponentData(root, status);
    }

    bool ValidPrefab(Entity entity, bool building)
    {
        var em = EntityManager;
        if (!em.Exists(entity) || !em.HasComponent<Prefab>(entity) || !em.HasComponent<LocalTransform>(entity) ||
            !em.HasComponent<EntityOwner>(entity) || !em.HasComponent<Selectable>(entity) || !em.HasComponent<EntityHealth>(entity) ||
            !math.isfinite(em.GetComponentData<EntityHealth>(entity).MaxHP) || em.GetComponentData<EntityHealth>(entity).MaxHP <= 0) return false;
        return building ? em.HasComponent<BuildingComponent>(entity) && em.HasComponent<BuildingConstruction>(entity) &&
            em.HasComponent<MainBaseTag>(entity) && em.HasComponent<BlockageData>(entity) && em.GetComponentData<BlockageData>(entity).CustomCost == 255 :
            em.HasComponent<UnitComponent>(entity) && em.HasComponent<MovementAgentComponent>(entity) && !em.HasComponent<BlockageData>(entity);
    }

    static bool TryPrefab(DynamicBuffer<RegistryPrefabElement> prefabs, int index, out Entity entity)
    {
        entity = index >= 0 && index < prefabs.Length ? prefabs[index].Prefab : Entity.Null;
        return entity != Entity.Null;
    }

    void Fail(Entity root, PlayerBootstrapState status, string reason)
    {
        status.Phase = PlayerBootstrapPhase.Failed;
        EntityManager.SetComponentData(root, status);
        Debug.LogError("Player bootstrap failed before spawning: " + reason);
    }
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateAfter(typeof(PopulationSystem))]
[UpdateAfter(typeof(BlockageGridBakeSystem))]
[UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(ProductionSystem))]
public partial class PlayerBootstrapReadySystem : SystemBase
{
    protected override void OnCreate() { RequireForUpdate<PlayerBootstrapState>(); RequireForUpdate<GridComponent>(); }
    protected override void OnUpdate()
    {
        var em = EntityManager;
        var root = SystemAPI.GetSingletonEntity<PlayerBootstrapState>();
        var status = em.GetComponentData<PlayerBootstrapState>(root);
        if (status.Phase != PlayerBootstrapPhase.Finalizing) return;
        var ge = SystemAPI.GetSingletonEntity<GridComponent>();
        var grid = em.GetComponentData<GridComponent>(ge);
        if (grid.generation == status.GridGeneration || grid.isDirty || grid.islandGeneration != grid.generation ||
            em.GetBuffer<CostChangeRequest>(ge).Length != 0) return;
        foreach (var spawn in em.GetBuffer<PlayerBootstrapSpawned>(root))
        {
            if (!em.Exists(spawn.Value)) return;
            if (em.HasComponent<ResourceNodeTag>(spawn.Value))
            {
                if (em.HasComponent<BlockageNeedBakeTag>(spawn.Value) && em.IsComponentEnabled<BlockageNeedBakeTag>(spawn.Value)) return;
                continue;
            }
            if (!em.HasComponent<PopulationAccount>(spawn.Value) ||
                em.GetComponentData<PopulationAccount>(spawn.Value).PlayerID != em.GetComponentData<EntityOwner>(spawn.Value).PlayerID ||
                em.HasComponent<SetupUnitMoverDefaultPosition>(spawn.Value) ||
                (em.HasComponent<BlockageNeedBakeTag>(spawn.Value) && em.IsComponentEnabled<BlockageNeedBakeTag>(spawn.Value))) return;
        }
        status.Phase = PlayerBootstrapPhase.Ready;
        em.SetComponentData(root, status);
    }
}

