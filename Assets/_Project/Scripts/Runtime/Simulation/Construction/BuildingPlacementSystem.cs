using System;
using System.Diagnostics;
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
        if (SystemAPI.TryGetSingleton<PlayerBootstrapState>(out var bootstrap) && bootstrap.Phase != PlayerBootstrapPhase.Ready) return;
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
            Debug.WriteLine(em.HasComponent<PlaceBuildingRequest>(reqEntity), "Entity in PlaceBuildingRequest query does not have PlaceBuildingRequest component");

            PlaceBuildingRequest req = em.GetComponentData<PlaceBuildingRequest>(reqEntity);

            if (req.PrefabEntity == Entity.Null || !em.Exists(req.PrefabEntity))
            {
                ecb.DestroyEntity(reqEntity);
                continue;
            }

            if (!em.HasComponent<BuildingComponent>(req.PrefabEntity) ||
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
                    if (!CommandAuthority.CanControl(em, req.PlayerId, entry.WorkerEntity)
                        || !em.HasBuffer<BuildOffer>(entry.WorkerEntity)) continue;
                    foreach (var offer in em.GetBuffer<BuildOffer>(entry.WorkerEntity))
                        if (offer.DefinitionID == em.GetComponentData<BuildingComponent>(req.PrefabEntity).DefinitionID) allowed = true;
                }
            if (!allowed) { ecb.DestroyEntity(reqEntity); continue; }

            using var costs = new NativeList<ResourcePair>(Allocator.Temp);
            foreach (var cost in em.GetBuffer<EntityResourceCost>(req.PrefabEntity))
                costs.Add(new ResourcePair(cost.Type, cost.Amount));
            if (!ResourcePayment.CanAfford(em, req.PlayerId, costs))
            {
                ecb.DestroyEntity(reqEntity);
                continue;
            }

            StartEndRect localRect;
            if (em.HasComponent<GridFootprint>(req.PrefabEntity))
                req.Position = GridSnapperMath.Snap(req.Position, grid, em.GetComponentData<GridFootprint>(req.PrefabEntity).Cells);
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

            var terrain = em.HasBuffer<GridTerrain>(gridEntity) ? em.GetBuffer<GridTerrain>(gridEntity).AsNativeArray() : default;
            var gridCosts = em.HasBuffer<GridNodeCost>(gridEntity) ? em.GetBuffer<GridNodeCost>(gridEntity).AsNativeArray() : default;
            if (BuildingPlacementValidator.Validate(grid, terrain, gridCosts, req.Position, localRect, unitBucket, out _, out _)
                != BuildingPlacementFailure.None)
            {
                ecb.DestroyEntity(reqEntity);
                continue;
            }

            Entity building = em.Instantiate(req.PrefabEntity);
            if (!ResourcePayment.TryPay(em, req.PlayerId, costs, out var paid))
            {
                paid.Dispose();
                em.DestroyEntity(building);
                ecb.DestroyEntity(reqEntity);
                continue;
            }
            bool reserved = false;
            Entity commandQueueEntity = Entity.Null;
            int commandQueueLength = -1;
            try
            {
            using (var commandQuery = em.CreateEntityQuery(ComponentType.ReadOnly<CommandQueueComponent>()))
                if (commandQuery.CalculateEntityCount() == 1)
                {
                    commandQueueEntity = commandQuery.GetSingletonEntity();
                    if (em.HasBuffer<CommandQueueElement>(commandQueueEntity))
                        commandQueueLength = em.GetBuffer<CommandQueueElement>(commandQueueEntity).Length;
                }
            em.AddComponentData(building, new BuildingConstruction { Phase = ConstructionPhase.Planned , CompletedWork=0  });
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

            reserved = true;
            ReserveGridArea(grid, em.GetBuffer<GridNodeCost>(gridEntity), req.Position, localRect, customCost);

            if (em.HasBuffer<PlaceBuildingWorkerElement>(reqEntity))
            {
                var workerBuffer = em.GetBuffer<PlaceBuildingWorkerElement>(reqEntity);
                for (int w = 0; w < workerBuffer.Length; w++)
                {
                    Entity worker = workerBuffer[w].WorkerEntity;
                    if (CommandAuthority.CanControl(em, req.PlayerId, worker)
                        && EntityCapabilities.CanBuild(em, worker, building) && em.HasComponent<BuilderComponent>(worker))
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
            paid.Dispose();
            gridModified = true;
            }
            catch (Exception ex)
            {
                if (commandQueueLength >= 0 && em.Exists(commandQueueEntity)
                    && em.HasBuffer<CommandQueueElement>(commandQueueEntity))
                    em.GetBuffer<CommandQueueElement>(commandQueueEntity).ResizeUninitialized(commandQueueLength);
                if (reserved)
                    ReserveGridArea(grid, em.GetBuffer<GridNodeCost>(gridEntity), req.Position, localRect, 1);
                ResourcePayment.Refund(em, req.PlayerId, paid);
                paid.Dispose();
                if (em.Exists(building))
                {
                    if (em.HasComponent<PopulationAccount>(building)) em.RemoveComponent<PopulationAccount>(building);
                    em.DestroyEntity(building);
                }
                ecb.DestroyEntity(reqEntity);
                UnityEngine.Debug.LogWarning($"Building placement rolled back after publication failed: {ex.Message}");
            }
        }

        if (gridModified)
        {
            grid.isDirty = true;
            grid.topologyVersion++;
            em.SetComponentData(gridEntity, grid);
        }

        ecb.Playback(em);
        ecb.Dispose();
    }

    private static void ReserveGridArea(GridComponent grid, DynamicBuffer<GridNodeCost> costBuffer, float3 rootPosition, StartEndRect localRect, int cost)
    {
        if (!PlayerSpawnPlacement.TryFootprint(grid, rootPosition, localRect, out int2 minGrid, out int2 maxGrid))
            return;

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


