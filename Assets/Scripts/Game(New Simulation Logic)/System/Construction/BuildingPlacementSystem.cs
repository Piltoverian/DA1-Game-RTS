using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(CommandQueue))]
[UpdateBefore(typeof(BlockageGridBakeSystem))]
public partial struct BuildingPlacementSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridComponent>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;
        if (!SystemAPI.TryGetSingletonEntity<GridComponent>(out Entity gridEntity))
            return;

        var grid = em.GetComponentData<GridComponent>(gridEntity);

        var query = em.CreateEntityQuery(typeof(PlaceBuildingRequest));
        if (query.IsEmpty)
            return;

        var bucketQuery = em.CreateEntityQuery(typeof(MovementAgentBucket));
        bool hasUnitBucket = !bucketQuery.IsEmpty;
        NativeParallelMultiHashMap<int, Entity> unitBucket = default;
        if (hasUnitBucket)
            unitBucket = bucketQuery.GetSingleton<MovementAgentBucket>().Bucket;

        var ecb = new EntityCommandBuffer(Allocator.Temp);
        bool gridModified = false;

        using var requests = query.ToEntityArray(Allocator.Temp);
        foreach (Entity reqEntity in requests)
        {
            PlaceBuildingRequest req = em.GetComponentData<PlaceBuildingRequest>(reqEntity);

            if (req.PrefabEntity == Entity.Null || !em.Exists(req.PrefabEntity))
            {
                ecb.DestroyEntity(reqEntity);
                continue;
            }

            if (!em.HasComponent<BuildingComponent>(req.PrefabEntity) ||
                !em.HasComponent<BuildingConstruction>(req.PrefabEntity) ||
                !em.HasComponent<EntityOwner>(req.PrefabEntity) ||
                !em.HasComponent<EntityHealth>(req.PrefabEntity) ||
                !em.HasBuffer<EntityResourceCost>(req.PrefabEntity) ||
                !math.isfinite(em.GetComponentData<BuildingComponent>(req.PrefabEntity).WorkLoad) ||
                em.GetComponentData<BuildingComponent>(req.PrefabEntity).WorkLoad <= 0f)
            {
                ecb.DestroyEntity(reqEntity);
                continue;
            }

            bool allowed = false;
            if (em.HasBuffer<PlaceBuildingWorkerElement>(reqEntity))
                foreach (var entry in em.GetBuffer<PlaceBuildingWorkerElement>(reqEntity))
                {
                    if (!em.Exists(entry.WorkerEntity) || !em.HasComponent<EntityOwner>(entry.WorkerEntity) || !em.HasBuffer<BuildOffer>(entry.WorkerEntity)
                        || em.GetComponentData<EntityOwner>(entry.WorkerEntity).PlayerID != req.PlayerId) continue;
                    foreach (var offer in em.GetBuffer<BuildOffer>(entry.WorkerEntity))
                        if (offer.DefinitionID == em.GetComponentData<BuildingComponent>(req.PrefabEntity).DefinitionID) allowed = true;
                }
            if (!allowed) { ecb.DestroyEntity(reqEntity); continue; }

            if (!CanAfford(em, req.PlayerId, req.PrefabEntity))
            {
                ecb.DestroyEntity(reqEntity);
                continue;
            }

            StartEndRect localRect;
            int customCost = 255;
            if (em.HasComponent<BlockageData>(req.PrefabEntity))
            {
                var blockage = em.GetComponentData<BlockageData>(req.PrefabEntity);
                localRect = blockage.LocalRect;
                customCost = blockage.CustomCost;
            }
            else
            {
                localRect = new StartEndRect(new float2(-1.5f, -1.5f));
                localRect.ExpandTo(new float2(1.5f, 1.5f));
            }

            if (!IsAreaFree(grid, em.GetBuffer<GridNodeCost>(gridEntity), req.Position, localRect, hasUnitBucket, unitBucket))
            {
                ecb.DestroyEntity(reqEntity);
                continue;
            }

            DeductCost(em, req.PlayerId, req.PrefabEntity);

            Entity building = em.Instantiate(req.PrefabEntity);
            em.SetComponentData(building, new BuildingConstruction { Phase = ConstructionPhase.Planned });
            em.AddComponentData(building, new PopulationAccount { PlayerID = -1 });

            if (em.HasComponent<LocalTransform>(building))
            {
                var lt = em.GetComponentData<LocalTransform>(building);
                lt.Position = req.Position;
                em.SetComponentData(building, lt);
            }
            else
            {
                em.AddComponentData(building, LocalTransform.FromPosition(req.Position));
            }

            if (em.HasComponent<EntityOwner>(building))
            {
                var u = em.GetComponentData<EntityOwner>(building);
                u.PlayerID = req.PlayerId;
                em.SetComponentData(building, u);
            }

            if (em.HasComponent<EntityHealth>(building))
            {
                var h = em.GetComponentData<EntityHealth>(building);
                h.CurrentHP = math.min(1f, h.MaxHP);
                h.Changed = true;
                em.SetComponentData(building, h);
            }

            if (em.HasComponent<BlockageNeedBakeTag>(building))
            {
                em.SetComponentEnabled<BlockageNeedBakeTag>(building, true);
            }
            else
            {
                em.AddComponent<BlockageNeedBakeTag>(building);
            }

            ReserveGridArea(grid, em.GetBuffer<GridNodeCost>(gridEntity), req.Position, localRect, customCost);
            gridModified = true;

            if (em.HasBuffer<PlaceBuildingWorkerElement>(reqEntity))
            {
                var workerBuffer = em.GetBuffer<PlaceBuildingWorkerElement>(reqEntity);
                for (int w = 0; w < workerBuffer.Length; w++)
                {
                    Entity worker = workerBuffer[w].WorkerEntity;
                    if (EntityCapabilities.CanBuild(em, worker, building) && em.HasComponent<BuilderComponent>(worker))
                    {
                        CommandDataHelper.AddCommandToQueue(
                            em,
                            req.PlayerId,
                            worker,
                            CommandType.Build,
                            targetEntity: building
                        );
                    }
                }
            }

            ecb.DestroyEntity(reqEntity);
        }

        if (gridModified)
        {
            grid.isDirty = true;
            em.SetComponentData(gridEntity, grid);
        }

        ecb.Playback(em);
        ecb.Dispose();
    }

    private static bool CanAfford(EntityManager em, int playerId, Entity prefabEntity)
    {
        if (playerId < 0)
            return false;

        if (!em.HasBuffer<EntityResourceCost>(prefabEntity))
            return true;

        var costBuffer = em.GetBuffer<EntityResourceCost>(prefabEntity);
        for (int i = 0; i < costBuffer.Length; i++)
        {
            var cost = costBuffer[i];
            if (PlayerContextHelper.GetPlayerResourceByType(em, playerId, cost.Type, out float currentAmount) != FunctionResult.Success)
                return false;

            if (currentAmount < cost.Amount)
                return false;
        }

        return true;
    }

    private static void DeductCost(EntityManager em, int playerId, Entity prefabEntity)
    {
        if (playerId < 0 || !em.HasBuffer<EntityResourceCost>(prefabEntity))
            return;

        var costBuffer = em.GetBuffer<EntityResourceCost>(prefabEntity);
        for (int i = 0; i < costBuffer.Length; i++)
        {
            var cost = costBuffer[i];
            if (PlayerContextHelper.GetPlayerResourceByType(em, playerId, cost.Type, out float currentAmount) == FunctionResult.Success)
            {
                PlayerContextHelper.SetPlayerResource(em, playerId, cost.Type, currentAmount - cost.Amount);
            }
        }
    }

    private static bool IsAreaFree(
        GridComponent grid,
        DynamicBuffer<GridNodeCost> costBuffer,
        float3 rootPosition,
        StartEndRect localRect,
        bool hasUnitBucket,
        NativeParallelMultiHashMap<int, Entity> unitBucket)
    {
        float2 worldMin = new float2(rootPosition.x + localRect.MinPoint.x, rootPosition.z + localRect.MinPoint.y);
        float2 worldMax = new float2(rootPosition.x + localRect.MaxPoint.x, rootPosition.z + localRect.MaxPoint.y);

        int2 minGrid = GridHelper.WorldToGrid(new float3(worldMin.x + 0.05f, 0, worldMin.y + 0.05f), grid);
        int2 maxGrid = GridHelper.WorldToGrid(new float3(worldMax.x - 0.05f, 0, worldMax.y - 0.05f), grid);

        for (int x = minGrid.x; x <= maxGrid.x; x++)
        {
            for (int y = minGrid.y; y <= maxGrid.y; y++)
            {
                if (x < 0 || x >= grid.width || y < 0 || y >= grid.height)
                    return false;

                int idx = GridHelper.GetNodeIndex(new int2(x, y), grid);
                if (costBuffer[idx].cost != 1)
                    return false;

                if (hasUnitBucket && unitBucket.IsCreated && unitBucket.ContainsKey(idx))
                    return false;
            }
        }

        return true;
    }

    private static void ReserveGridArea(GridComponent grid, DynamicBuffer<GridNodeCost> costBuffer, float3 rootPosition, StartEndRect localRect, int cost)
    {
        float2 worldMin = new float2(rootPosition.x + localRect.MinPoint.x, rootPosition.z + localRect.MinPoint.y);
        float2 worldMax = new float2(rootPosition.x + localRect.MaxPoint.x, rootPosition.z + localRect.MaxPoint.y);

        int2 minGrid = GridHelper.WorldToGrid(new float3(worldMin.x + 0.05f, 0, worldMin.y + 0.05f), grid);
        int2 maxGrid = GridHelper.WorldToGrid(new float3(worldMax.x - 0.05f, 0, worldMax.y - 0.05f), grid);

        for (int x = minGrid.x; x <= maxGrid.x; x++)
        {
            for (int y = minGrid.y; y <= maxGrid.y; y++)
            {
                if (x < 0 || x >= grid.width || y < 0 || y >= grid.height)
                    continue;

                int idx = GridHelper.GetNodeIndex(new int2(x, y), grid);
                GridNodeCost node = costBuffer[idx];
                node.cost = cost;
                costBuffer[idx] = node;
            }
        }
    }
}


