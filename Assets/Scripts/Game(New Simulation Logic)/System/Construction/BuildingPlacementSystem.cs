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
        var gridCostBuffer = em.GetBuffer<GridNodeCost>(gridEntity);

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

        using (var entities = query.ToEntityArray(Allocator.Temp))
        using (var requests = query.ToComponentDataArray<PlaceBuildingRequest>(Allocator.Temp))
        {
            for (int i = 0; i < entities.Length; i++)
            {
                Entity reqEntity = entities[i];
                PlaceBuildingRequest req = requests[i];

                if (req.PrefabEntity == Entity.Null || !em.Exists(req.PrefabEntity))
                {
                    ecb.DestroyEntity(reqEntity);
                    continue;
                }

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

                if (!IsAreaFree(grid, gridCostBuffer, req.Position, localRect, hasUnitBucket, unitBucket))
                {
                    ecb.DestroyEntity(reqEntity);
                    continue;
                }

                DeductCost(em, req.PlayerId, req.PrefabEntity);

                Entity building = em.Instantiate(req.PrefabEntity);

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

                if (em.HasComponent<Unit>(building))
                {
                    var u = em.GetComponentData<Unit>(building);
                    u.playerID = req.PlayerId;
                    em.SetComponentData(building, u);
                }

                if (em.HasComponent<BuildingData>(building))
                {
                    var bd = em.GetComponentData<BuildingData>(building);
                    if (req.TotalWorkLoad > 0f)
                        bd.TotalWorkLoad = req.TotalWorkLoad;
                    em.SetComponentData(building, bd);
                }

                if (em.HasComponent<ConstructionData>(building))
                {
                    em.SetComponentData(building, new ConstructionData { currentWorkLoad = 0f });
                }

                if (!em.HasComponent<UnderConstructionTag>(building))
                {
                    em.AddComponent<UnderConstructionTag>(building);
                }

                if (em.HasComponent<BlockageNeedBakeTag>(building))
                {
                    em.SetComponentEnabled<BlockageNeedBakeTag>(building, true);
                }
                else
                {
                    em.AddComponent<BlockageNeedBakeTag>(building);
                }

                ReserveGridArea(grid, gridCostBuffer, req.Position, localRect, customCost);
                gridModified = true;

                if (em.HasBuffer<PlaceBuildingWorkerElement>(reqEntity))
                {
                    var workerBuffer = em.GetBuffer<PlaceBuildingWorkerElement>(reqEntity);
                    for (int w = 0; w < workerBuffer.Length; w++)
                    {
                        Entity worker = workerBuffer[w].WorkerEntity;
                        if (worker != Entity.Null && em.Exists(worker) && em.HasComponent<BuilderComponent>(worker))
                        {
                            CommandDataHelper.AddCommandToQueue(
                                em,
                                req.PlayerId,
                                worker,
                                new CommandData
                                {
                                    Type = CommandType.Build,
                                    indexInUnitCommandList = 0
                                },
                                targetEntity: building
                            );
                        }
                    }
                }

                ecb.DestroyEntity(reqEntity);
            }
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

        if (!em.HasBuffer<BuildingCost>(prefabEntity))
            return true;

        var costBuffer = em.GetBuffer<BuildingCost>(prefabEntity);
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
        if (playerId < 0 || !em.HasBuffer<BuildingCost>(prefabEntity))
            return;

        var costBuffer = em.GetBuffer<BuildingCost>(prefabEntity);
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
