using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>Small 1v1 opponent. Uses normal paid build/train/gather/attack orders, with bounded army size.</summary>
public sealed class SkirmishOpponent
{
    private float nextEconomy, nextTrain = 25, nextRaid = 100, nextBuild = 18;
    private readonly List<Entity> workers = new(), army = new(), producers = new();
    public void Reset() { nextEconomy = 0; nextTrain = 25; nextRaid = 100; nextBuild = 18; }
    public void Tick(EntityManager em, int player, Entity home, Entity target, float elapsed)
    {
        if (elapsed < nextEconomy) return;
        nextEconomy = elapsed + 2;
        workers.Clear(); army.Clear(); producers.Clear(); bool barracks = false;
        using (var query = em.CreateEntityQuery(typeof(EntityOwner), typeof(EntityHealth)))
        using (var entities = query.ToEntityArray(Allocator.Temp))
        {
            foreach (var entity in entities)
            {
                if (em.GetComponentData<EntityOwner>(entity).PlayerID != player || em.GetComponentData<EntityHealth>(entity).CurrentHP <= 0) continue;
                if (em.HasComponent<WorkerTag>(entity)) workers.Add(entity);
                else if (em.HasComponent<UnitComponent>(entity) && em.HasComponent<ShootAttack>(entity)) army.Add(entity);
                if (em.HasBuffer<ProductionElement>(entity) && (!em.HasComponent<BuildingConstruction>(entity) ||
                    em.GetComponentData<BuildingConstruction>(entity).Phase == ConstructionPhase.Completed)) producers.Add(entity);
                if (em.HasComponent<BuildingComponent>(entity) && em.GetComponentData<BuildingComponent>(entity).DefinitionID == "building.barracks") barracks = true;
            }
        }
        using (var nodes = em.CreateEntityQuery(typeof(ResourceNodeData), typeof(LocalTransform)))
        using (var resources = nodes.ToEntityArray(Allocator.Temp))
        {
            for (int i = 0; i < workers.Count; i++)
            {
                var worker = workers[i];
                if (em.HasComponent<BuilderComponent>(worker) && em.IsComponentEnabled<BuilderComponent>(worker)) continue;
                if (em.IsComponentEnabled<WorkerGatherData>(worker)) continue;
                ResourceType wanted = i % 5 == 4 ? ResourceType.Food : i % 5 == 3 ? ResourceType.Wood : ResourceType.Gold;
                Entity nearest = Entity.Null; float distance = float.MaxValue;
                var position = em.GetComponentData<LocalTransform>(worker).Position;
                foreach (var resource in resources)
                {
                    var data = em.GetComponentData<ResourceNodeData>(resource);
                    if (data.Type != wanted || data.Amount <= 0 || em.HasComponent<ResourceNodePendingConfig>(resource)) continue;
                    float d = math.distancesq(position, em.GetComponentData<LocalTransform>(resource).Position);
                    if (d < distance) { nearest = resource; distance = d; }
                }
                if (nearest != Entity.Null) CommandDataHelper.AddCommandToQueue(em, player, worker, CommandType.TargetTo, targetEntity: nearest);
            }
        }
        if (!barracks && workers.Count > 0 && elapsed >= nextBuild)
        {
            nextBuild = elapsed + 18;
            Entity prefab = FindBuildingPrefab(em, "building.barracks");
            if (prefab != Entity.Null && TryBuildPosition(em, prefab, home, out var position))
                CommandDataHelper.AddPlanBuildToQueue(em, player, new[] { workers[0] }, prefab, position);
        }
        if (elapsed >= nextTrain && army.Count < 8)
        {
            nextTrain = elapsed + 10;
            int queued = 0;
            foreach (var producer in producers) if (em.HasBuffer<ProductionQueueElement>(producer)) queued += em.GetBuffer<ProductionQueueElement>(producer).Length;
            if (army.Count + queued < 8)
            {
                foreach (var producer in producers)
                {
                    if (em.HasBuffer<ProductionQueueElement>(producer) && em.GetBuffer<ProductionQueueElement>(producer).Length >= 1) continue;
                    var offers = em.GetBuffer<ProductionElement>(producer);
                    foreach (var offer in offers)
                        if (offer.Kind == ProductionKind.Train && offer.JobID == "dogfood.train.soldier")
                        { CommandDataHelper.AddCommandToQueue(em, player, producer, CommandType.Progression, progression: offer.JobReference); break; }
                    break;
                }
            }
        }
        if (elapsed >= nextRaid && army.Count >= 3 && em.Exists(target))
        {
            nextRaid = elapsed + 35;
            foreach (var soldier in army) CommandDataHelper.AddCommandToQueue(em, player, soldier, CommandType.TargetTo, targetEntity: target);
        }
    }
    public static Entity FindBuildingPrefab(EntityManager em, FixedString64Bytes id)
    {
        if (!ProductionJobs.TryRegistry(em, out var registry)) return Entity.Null;
        var definitions = em.GetBuffer<RegistryBlobElement>(registry, true);
        var definition = definitions.GetBlobByID<BuildingBlob>(id, out var found);
        if (found != FunctionResult.Success) return Entity.Null;
        var prefabs = em.GetBuffer<RegistryPrefabElement>(registry, true); int index = definition.Value.Base.PrefabIndex;
        return index >= 0 && index < prefabs.Length ? prefabs[index].Prefab : Entity.Null;
    }
    public static bool TryBuildPosition(EntityManager em, Entity prefab, Entity home, out float3 position)
    {
        position = default;
        if (!em.Exists(prefab) || !em.HasComponent<BlockageData>(prefab)) return false;
        using var grids = em.CreateEntityQuery(typeof(GridComponent), typeof(GridTerrain), typeof(GridNodeCost));
        using var spatial = em.CreateEntityQuery(typeof(UnitSpatialBucket));
        if (grids.CalculateEntityCount() != 1 || spatial.CalculateEntityCount() != 1) return false;
        var root = grids.GetSingletonEntity(); var grid = grids.GetSingleton<GridComponent>();
        var terrain = em.GetBuffer<GridTerrain>(root, true).AsNativeArray(); var costs = em.GetBuffer<GridNodeCost>(root, true).AsNativeArray();
        int cells = em.GetComponentData<GridFootprint>(prefab).Cells;
        var center = GridHelper.WorldToGrid(em.GetComponentData<LocalTransform>(home).Position, grid);
        for (int ring = 8; ring <= 18; ring += 2)
        for (int y = -ring; y <= ring; y += 2)
        for (int x = -ring; x <= ring; x += 2)
        {
            if (math.max(math.abs(x), math.abs(y)) != ring) continue;
            var candidate = GridSnapperMath.Snap(GridHelper.GridToWorld(center + new int2(x, y), grid), grid, cells);
            candidate.y = em.GetComponentData<LocalTransform>(home).Position.y;
            if (BuildingPlacementValidator.Validate(grid, terrain, costs, candidate, em.GetComponentData<BlockageData>(prefab).LocalRect,
                spatial.GetSingleton<UnitSpatialBucket>().Bucket, out _, out _) != BuildingPlacementFailure.None) continue;
            position = candidate; return true;
        }
        return false;
    }
}
