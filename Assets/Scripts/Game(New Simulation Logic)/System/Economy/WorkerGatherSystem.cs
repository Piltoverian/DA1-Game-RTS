using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public partial struct WorkerGatherSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;

        Entity gridEntity = SystemAPI.GetSingletonEntity<GridComponent>();
        GridComponent grid = SystemAPI.GetComponent<GridComponent>(gridEntity);
        DynamicBuffer<GridIsland> islandBuffer = SystemAPI.GetBuffer<GridIsland>(gridEntity);

        var nodeLookup = SystemAPI.GetComponentLookup<ResourceNodeData>(false);
        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        var blockageLookup = SystemAPI.GetComponentLookup<BlockageData>(true);
        var agentLookup = SystemAPI.GetComponentLookup<MovementAgentComponent>(false);
        var steeringLookup = SystemAPI.GetComponentLookup<MovementSteeringComponent>(false);
        var moveOverrideLookup = SystemAPI.GetComponentLookup<MoveOverride>(true);
        var resourceBufferLookup = SystemAPI.GetBufferLookup<ResourcePair>(false);

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        var playerEntityMap = new NativeHashMap<int, Entity>(8, Allocator.Temp);
        foreach (var (playerContext, entity) in SystemAPI.Query<RefRO<PlayerContext>>().WithEntityAccess())
        {
            playerEntityMap.TryAdd(playerContext.ValueRO.PlayerId, entity);
        }

        var depotCache = new NativeParallelMultiHashMap<DepotKey, DepotInfo>(32, Allocator.Temp);
        foreach (var (transform, unit, bState, entity) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<EntityOwner>, RefRO<BuildingConstruction>>()
                     .WithAll<StorageResource>()
                     .WithEntityAccess())
        {
            if (bState.ValueRO.Phase != ConstructionPhase.Completed) continue;

            float3 pos = transform.ValueRO.Position;
            if (blockageLookup.HasComponent(entity))
            {
                pos = blockageLookup[entity].GetWorldCenter(pos);
            }
            int islandID = GetIslandID(pos, grid, islandBuffer);
            DepotInfo info = new DepotInfo
            {
                Entity = entity,
                Position = pos,
                IslandID = islandID
            };

            foreach (var resource in SystemAPI.GetBuffer<StorageResource>(entity))
                depotCache.Add(new DepotKey { PlayerId = unit.ValueRO.PlayerID, ResourceType = resource.Type }, info);
        }

        var nodeSpatialCache = new NativeParallelMultiHashMap<int, ResourceNodeSpatialInfo>(1024, Allocator.Temp);
        foreach (var (node, transform, entity) in SystemAPI.Query<RefRO<ResourceNodeData>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            if (node.ValueRO.Amount <= 0) continue;
            float3 pos = transform.ValueRO.Position;
            if (blockageLookup.HasComponent(entity))
            {
                pos = blockageLookup[entity].GetWorldCenter(pos);
            }
            int2 gridPos = GridHelper.WorldToGrid(pos, grid);
            if (gridPos.x < 0 || gridPos.x >= grid.width || gridPos.y < 0 || gridPos.y >= grid.height) continue;
            int cellIndex = GridHelper.GetNodeIndex(gridPos, grid);
            int islandID = islandBuffer[cellIndex].islandID;

            nodeSpatialCache.Add(cellIndex, new ResourceNodeSpatialInfo
            {
                Entity = entity,
                Type = node.ValueRO.Type,
                Amount = node.ValueRO.Amount,
                IslandID = islandID,
                Position = pos
            });
        }

        foreach (var (workerTransform, unit, gather, targetCache, workerEntity) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRO<EntityOwner>, RefRW<WorkerGatherData>, RefRW<TargetCache>>()
                     .WithAll<WorkerTag>()
                     .WithEntityAccess())
        {
            if (SystemAPI.HasComponent<EntityWork>(workerEntity)) gather.ValueRW.WorkRate = SystemAPI.GetComponent<EntityWork>(workerEntity).Rate;
            float3 workerPos = workerTransform.ValueRO.Position;
            int workerIsland = GetIslandID(workerPos, grid, islandBuffer);

            switch (gather.ValueRO.State)
            {
                case WorkerGatherState.Idle:
                    break;

                case WorkerGatherState.GoingToNode:
                    ExecuteGoingToNode(
                        gather,
                        targetCache,
                        workerEntity,
                        workerPos,
                        workerIsland,
                        ref nodeLookup,
                        ref transformLookup,
                        ref blockageLookup,
                        ref agentLookup,
                        ref steeringLookup,
                        ref moveOverrideLookup,
                        grid,
                        ref nodeSpatialCache,
                        ref ecb);
                    break;

                case WorkerGatherState.Gathering:
                    ExecuteGathering(
                        gather,
                        targetCache,
                        workerEntity,
                        workerPos,
                        workerIsland,
                        dt,
                        ref nodeLookup,
                        ref transformLookup,
                        ref blockageLookup,
                        ref agentLookup,
                        ref steeringLookup,
                        grid,
                        ref nodeSpatialCache,
                        ref ecb);
                    break;

                case WorkerGatherState.ReturningDepot:
                    ExecuteReturningDepot(
                        gather,
                        targetCache,
                        workerEntity,
                        unit.ValueRO.PlayerID,
                        workerPos,
                        workerIsland,
                        ref nodeLookup,
                        ref transformLookup,
                        ref blockageLookup,
                        ref agentLookup,
                        ref steeringLookup,
                        ref moveOverrideLookup,
                        ref resourceBufferLookup,
                        ref playerEntityMap,
                        grid,
                        ref depotCache,
                        ref nodeSpatialCache,
                        ref ecb);
                    break;
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();

        playerEntityMap.Dispose();
        depotCache.Dispose();
        nodeSpatialCache.Dispose();
    }

    private static void ExecuteGoingToNode(
        RefRW<WorkerGatherData> gather,
        RefRW<TargetCache> targetCache,
        Entity workerEntity,
        float3 workerPos,
        int workerIsland,
        ref ComponentLookup<ResourceNodeData> nodeLookup,
        ref ComponentLookup<LocalTransform> transformLookup,
        ref ComponentLookup<BlockageData> blockageLookup,
        ref ComponentLookup<MovementAgentComponent> agentLookup,
        ref ComponentLookup<MovementSteeringComponent> steeringLookup,
        ref ComponentLookup<MoveOverride> moveOverrideLookup,
        in GridComponent grid,
        ref NativeParallelMultiHashMap<int, ResourceNodeSpatialInfo> nodeSpatialCache,
        ref EntityCommandBuffer ecb)
    {
        Entity targetNode = gather.ValueRO.TargetNode;

        if (targetNode == Entity.Null || !nodeLookup.HasComponent(targetNode) || nodeLookup[targetNode].Amount <= 0)
        {
            float3 searchCenter = math.lengthsq(targetCache.ValueRO.lastTargetPosition) > 0.001f ? targetCache.ValueRO.lastTargetPosition : workerPos;
            Entity altNode = FindNearestNodeSpatial(searchCenter, gather.ValueRO.CurrentResourceType, workerIsland, grid, ref nodeSpatialCache);
            if (altNode != Entity.Null)
            {
                gather.ValueRW.TargetNode = altNode;
                targetCache.ValueRW.targetEntity = altNode;
                targetNode = altNode;
            }
            else
            {
                if (gather.ValueRO.CarryAmount > 0)
                {
                    gather.ValueRW.State = WorkerGatherState.ReturningDepot;
                }
                else
                {
                    StopMoving(ref ecb, workerEntity, ref agentLookup, ref steeringLookup);
                    gather.ValueRW.TargetNode = Entity.Null;
                    targetCache.ValueRW.targetEntity = Entity.Null;
                    gather.ValueRW.State = WorkerGatherState.Idle;
                }
                return;
            }
        }

        if (!transformLookup.HasComponent(targetNode))
        {
            gather.ValueRW.TargetNode = Entity.Null;
            targetCache.ValueRW.targetEntity = Entity.Null;
            gather.ValueRW.State = WorkerGatherState.Idle;
            StopMoving(ref ecb, workerEntity, ref agentLookup, ref steeringLookup);
            return;
        }

        float3 rawNodePos = transformLookup[targetNode].Position;
        float3 nodePos = blockageLookup.HasComponent(targetNode)
            ? blockageLookup[targetNode].GetWorldCenter(rawNodePos)
            : rawNodePos;

        targetCache.ValueRW.lastTargetPosition = nodePos;
        targetCache.ValueRW.targetEntity = targetNode;

        if (HasReachedTarget(workerPos, targetNode, rawNodePos, gather.ValueRO.StopDistanceSq, ref blockageLookup))
        {
            StopMoving(ref ecb, workerEntity, ref agentLookup, ref steeringLookup);
            gather.ValueRW.State = WorkerGatherState.Gathering;
            gather.ValueRW.GatherTimer = gather.ValueRO.GatherTime;
        }
        else
        {
            MoveTo(ref ecb, workerEntity, nodePos, ref moveOverrideLookup);
        }
    }

    private static void ExecuteGathering(
        RefRW<WorkerGatherData> gather,
        RefRW<TargetCache> targetCache,
        Entity workerEntity,
        float3 workerPos,
        int workerIsland,
        float dt,
        ref ComponentLookup<ResourceNodeData> nodeLookup,
        ref ComponentLookup<LocalTransform> transformLookup,
        ref ComponentLookup<BlockageData> blockageLookup,
        ref ComponentLookup<MovementAgentComponent> agentLookup,
        ref ComponentLookup<MovementSteeringComponent> steeringLookup,
        in GridComponent grid,
        ref NativeParallelMultiHashMap<int, ResourceNodeSpatialInfo> nodeSpatialCache,
        ref EntityCommandBuffer ecb)
    {
        Entity targetNode = gather.ValueRO.TargetNode;

        if (targetNode == Entity.Null || !nodeLookup.HasComponent(targetNode) || nodeLookup[targetNode].Amount <= 0)
        {
            if (gather.ValueRO.CarryAmount > 0)
            {
                gather.ValueRW.State = WorkerGatherState.ReturningDepot;
            }
            else
            {
                float3 searchCenter = math.lengthsq(targetCache.ValueRO.lastTargetPosition) > 0.001f ? targetCache.ValueRO.lastTargetPosition : workerPos;
                Entity altNode = FindNearestNodeSpatial(searchCenter, gather.ValueRO.CurrentResourceType, workerIsland, grid, ref nodeSpatialCache);
                if (altNode != Entity.Null)
                {
                    gather.ValueRW.TargetNode = altNode;
                    targetCache.ValueRW.targetEntity = altNode;
                    if (transformLookup.HasComponent(altNode))
                    {
                        float3 altRaw = transformLookup[altNode].Position;
                        targetCache.ValueRW.lastTargetPosition = blockageLookup.HasComponent(altNode)
                            ? blockageLookup[altNode].GetWorldCenter(altRaw)
                            : altRaw;
                    }
                    gather.ValueRW.State = WorkerGatherState.GoingToNode;
                }
                else
                {
                    StopMoving(ref ecb, workerEntity, ref agentLookup, ref steeringLookup);
                    gather.ValueRW.TargetNode = Entity.Null;
                    targetCache.ValueRW.targetEntity = Entity.Null;
                    gather.ValueRW.State = WorkerGatherState.Idle;
                }
            }
            return;
        }

        if (transformLookup.HasComponent(targetNode))
        {
            float3 rawPos = transformLookup[targetNode].Position;
            targetCache.ValueRW.lastTargetPosition = blockageLookup.HasComponent(targetNode)
                ? blockageLookup[targetNode].GetWorldCenter(rawPos)
                : rawPos;
            targetCache.ValueRW.targetEntity = targetNode;
        }

        gather.ValueRW.GatherTimer -= dt * gather.ValueRO.WorkRate;
        if (gather.ValueRO.GatherTimer > 0f)
            return;

        var node = nodeLookup[targetNode];
        int spaceLeft = gather.ValueRO.Capacity - gather.ValueRO.CarryAmount;
        float gathered = gather.ValueRO.GatherFraction + gather.ValueRO.GatherRate;
        int amount = math.min(math.min(spaceLeft, node.Amount), (int)math.floor(gathered));
        gather.ValueRW.GatherFraction = gathered - math.floor(gathered);

        node.Amount -= amount;
        nodeLookup[targetNode] = node;

        gather.ValueRW.CarryAmount += amount;
        gather.ValueRW.CurrentResourceType = node.Type;

        if (gather.ValueRO.CarryAmount >= gather.ValueRO.Capacity || node.Amount <= 0)
        {
            gather.ValueRW.State = WorkerGatherState.ReturningDepot;
        }
        else
        {
            gather.ValueRW.GatherTimer = gather.ValueRO.GatherTime;
        }
    }

    private static void ExecuteReturningDepot(
        RefRW<WorkerGatherData> gather,
        RefRW<TargetCache> targetCache,
        Entity workerEntity,
        int playerId,
        float3 workerPos,
        int workerIsland,
        ref ComponentLookup<ResourceNodeData> nodeLookup,
        ref ComponentLookup<LocalTransform> transformLookup,
        ref ComponentLookup<BlockageData> blockageLookup,
        ref ComponentLookup<MovementAgentComponent> agentLookup,
        ref ComponentLookup<MovementSteeringComponent> steeringLookup,
        ref ComponentLookup<MoveOverride> moveOverrideLookup,
        ref BufferLookup<ResourcePair> resourceBufferLookup,
        ref NativeHashMap<int, Entity> playerEntityMap,
        in GridComponent grid,
        ref NativeParallelMultiHashMap<DepotKey, DepotInfo> depotCache,
        ref NativeParallelMultiHashMap<int, ResourceNodeSpatialInfo> nodeSpatialCache,
        ref EntityCommandBuffer ecb)
    {
        Entity targetDepot = gather.ValueRO.TargetDepot;

        bool depotValid = false;
        var depotKey = new DepotKey { PlayerId = playerId, ResourceType = gather.ValueRO.CurrentResourceType };
        if (depotCache.TryGetFirstValue(depotKey, out var candidateDepot, out var depotIterator))
            do { if (candidateDepot.Entity == targetDepot) depotValid = true; } while (depotCache.TryGetNextValue(out candidateDepot, ref depotIterator));
        if (!depotValid || targetDepot == Entity.Null || !transformLookup.HasComponent(targetDepot))
        {
            targetDepot = FindNearestDepot(workerPos, playerId, gather.ValueRO.CurrentResourceType, workerIsland, ref depotCache);
            gather.ValueRW.TargetDepot = targetDepot;

            if (targetDepot == Entity.Null)
            {
                StopMoving(ref ecb, workerEntity, ref agentLookup, ref steeringLookup);
                gather.ValueRW.TargetNode = Entity.Null;
                targetCache.ValueRW.targetEntity = Entity.Null;
                gather.ValueRW.State = WorkerGatherState.Idle;
                return;
            }
        }

        float3 rawDepotPos = transformLookup[targetDepot].Position;
        float3 depotPos = blockageLookup.HasComponent(targetDepot)
            ? blockageLookup[targetDepot].GetWorldCenter(rawDepotPos)
            : rawDepotPos;

        if (HasReachedTarget(workerPos, targetDepot, rawDepotPos, gather.ValueRO.StopDistanceSq, ref blockageLookup))
        {
            StopMoving(ref ecb, workerEntity, ref agentLookup, ref steeringLookup);

            bool deposited = false;
            if (playerEntityMap.TryGetValue(playerId, out Entity playerEntity) && resourceBufferLookup.HasBuffer(playerEntity))
            {
                var buffer = resourceBufferLookup[playerEntity];
                for (int i = 0; i < buffer.Length; i++)
                {
                    if (buffer[i].Type == gather.ValueRO.CurrentResourceType)
                    {
                        buffer[i] = new ResourcePair(buffer[i].Type, buffer[i].Amount + gather.ValueRO.CarryAmount);
                        deposited = true;
                        break;
                    }
                }
            }

            if (!deposited) return;
            gather.ValueRW.CarryAmount = 0;

            Entity oldNode = gather.ValueRO.TargetNode;
            if (oldNode != Entity.Null && nodeLookup.HasComponent(oldNode) && nodeLookup[oldNode].Amount > 0)
            {
                gather.ValueRW.State = WorkerGatherState.GoingToNode;
            }
            else
            {
                float3 searchCenter = math.lengthsq(targetCache.ValueRO.lastTargetPosition) > 0.001f ? targetCache.ValueRO.lastTargetPosition : workerPos;
                Entity altNode = FindNearestNodeSpatial(searchCenter, gather.ValueRO.CurrentResourceType, workerIsland, grid, ref nodeSpatialCache);
                if (altNode != Entity.Null)
                {
                    gather.ValueRW.TargetNode = altNode;
                    targetCache.ValueRW.targetEntity = altNode;
                    if (transformLookup.HasComponent(altNode))
                    {
                        float3 altRaw = transformLookup[altNode].Position;
                        targetCache.ValueRW.lastTargetPosition = blockageLookup.HasComponent(altNode)
                            ? blockageLookup[altNode].GetWorldCenter(altRaw)
                            : altRaw;
                    }
                    gather.ValueRW.State = WorkerGatherState.GoingToNode;
                }
                else
                {
                    gather.ValueRW.TargetNode = Entity.Null;
                    gather.ValueRW.TargetDepot = Entity.Null;
                    targetCache.ValueRW.targetEntity = Entity.Null;
                    gather.ValueRW.State = WorkerGatherState.Idle;
                }
            }
        }
        else
        {
            MoveTo(ref ecb, workerEntity, depotPos, ref moveOverrideLookup);
        }
    }

    private static bool HasReachedTarget(
        float3 workerPos,
        Entity targetEntity,
        float3 targetPos,
        float stopDistanceSq,
        ref ComponentLookup<BlockageData> blockageLookup)
    {
        float reachDist = math.sqrt(stopDistanceSq);
        float reachDistSq = reachDist * reachDist;

        if (blockageLookup.HasComponent(targetEntity))
        {
            var blockage = blockageLookup[targetEntity];
            float2 worldMin = targetPos.xz + blockage.LocalRect.MinPoint;
            float2 worldMax = targetPos.xz + blockage.LocalRect.MaxPoint;
            float2 closestPoint = math.clamp(workerPos.xz, worldMin, worldMax);
            return math.distancesq(workerPos.xz, closestPoint) <= reachDistSq;
        }

        return math.distancesq(workerPos.xz, targetPos.xz) <= reachDistSq;
    }

    private static void MoveTo(
        ref EntityCommandBuffer ecb, 
        Entity entity, 
        float3 target, 
        ref ComponentLookup<MoveOverride> moveOverrideLookup)
    {
        if (moveOverrideLookup.HasComponent(entity) && moveOverrideLookup.IsComponentEnabled(entity))
        {
            var current = moveOverrideLookup[entity];
            if (math.distancesq(current.targetPosition.xz, target.xz) < 0.01f)
            {
                return;
            }
        }

        ecb.SetComponent(entity, new MoveOverride
        {
            targetPosition = target,
            targetApplied = false
        });
        ecb.SetComponentEnabled<MoveOverride>(entity, true);
    }

    private static void StopMoving(
        ref EntityCommandBuffer ecb,
        Entity entity,
        ref ComponentLookup<MovementAgentComponent> agentLookup,
        ref ComponentLookup<MovementSteeringComponent> steeringLookup)
    {
        ecb.SetComponentEnabled<MoveOverride>(entity, false);

        if (agentLookup.HasComponent(entity))
        {
            var agent = agentLookup[entity];
            agent.hastarget = false;
            agent.velocity = float3.zero;
            ecb.SetComponent(entity, agent);
        }

        if (steeringLookup.HasComponent(entity))
        {
            var steering = steeringLookup[entity];
            steering.isSettled = true;
            steering.stuckTime = 0;
            steering.minDistanceToTarget = float.MaxValue;
            ecb.SetComponent(entity, steering);
        }
    }

    private static Entity FindNearestDepot(
        float3 workerPos,
        int playerId,
        ResourceType resourceType,
        int workerIsland,
        ref NativeParallelMultiHashMap<DepotKey, DepotInfo> depotCache)
    {
        DepotKey key = new DepotKey { PlayerId = playerId, ResourceType = resourceType };
        Entity bestDepot = Entity.Null;
        float bestDistSq = float.MaxValue;

        if (depotCache.TryGetFirstValue(key, out var depotInfo, out var it))
        {
            do
            {
                if (workerIsland == 0 || depotInfo.IslandID == 0 || depotInfo.IslandID == workerIsland)
                {
                    float distSq = math.distancesq(workerPos, depotInfo.Position);
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        bestDepot = depotInfo.Entity;
                    }
                }
            } while (depotCache.TryGetNextValue(out depotInfo, ref it));
        }

        return bestDepot;
    }

    private static Entity FindNearestNodeSpatial(
        float3 centerPos,
        ResourceType requiredType,
        int workerIsland,
        in GridComponent grid,
        ref NativeParallelMultiHashMap<int, ResourceNodeSpatialInfo> nodeSpatialCache)
    {
        const int cellRadius = 5;
        int2 centerCell = GridHelper.WorldToGrid(centerPos, grid);

        Entity bestNode = Entity.Null;
        float bestDistSq = float.MaxValue;

        int minX = math.max(0, centerCell.x - cellRadius);
        int maxX = math.min(grid.width - 1, centerCell.x + cellRadius);
        int minY = math.max(0, centerCell.y - cellRadius);
        int maxY = math.min(grid.height - 1, centerCell.y + cellRadius);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int cellIndex = y * grid.width + x;
                if (nodeSpatialCache.TryGetFirstValue(cellIndex, out var nodeInfo, out var it))
                {
                    do
                    {
                        if (nodeInfo.Type == requiredType && nodeInfo.Amount > 0)
                        {
                            if (workerIsland == 0 || nodeInfo.IslandID == 0 || nodeInfo.IslandID == workerIsland)
                            {
                                float distSq = math.distancesq(centerPos, nodeInfo.Position);
                                if (distSq < bestDistSq)
                                {
                                    bestDistSq = distSq;
                                    bestNode = nodeInfo.Entity;
                                }
                            }
                        }
                    } while (nodeSpatialCache.TryGetNextValue(out nodeInfo, ref it));
                }
            }
        }

        return bestNode;
    }

    private static int GetIslandID(float3 pos, in GridComponent grid, in DynamicBuffer<GridIsland> islandBuffer)
    {
        int2 gridPos = GridHelper.WorldToGrid(pos, grid);
        if (gridPos.x >= 0 && gridPos.x < grid.width && gridPos.y >= 0 && gridPos.y < grid.height)
        {
            int index = GridHelper.GetNodeIndex(gridPos, grid);
            if (index >= 0 && index < islandBuffer.Length)
            {
                return islandBuffer[index].islandID;
            }
        }
        return 0;
    }
}


