using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public partial struct WorkerGatherSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;

        var nodeLookup = SystemAPI.GetComponentLookup<ResourceNodeData>(false);
        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);

        if (!SystemAPI.TryGetSingletonEntity<PlayerResourceData>(out Entity resourceEntity))
        {
            Debug.LogError("[WorkerGather] Không tìm thấy PlayerResourceData singleton");
            return;
        }

        RefRW<PlayerResourceData> playerResource =
            SystemAPI.GetComponentRW<PlayerResourceData>(resourceEntity);

        var ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        int workerCount = 0;

        foreach (var (workerTransform, gather, workerEntity) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRW<WorkerGatherData>>()
                     .WithAll<WorkerTag>()
                     .WithEntityAccess())
        {
            workerCount++;

            float3 workerPos = workerTransform.ValueRO.Position;

            bool needFindNode =
                gather.ValueRO.TargetNode == Entity.Null ||
                !nodeLookup.HasComponent(gather.ValueRO.TargetNode) ||
                nodeLookup[gather.ValueRO.TargetNode].Amount <= 0;

            if (needFindNode)
            {
                if (gather.ValueRO.TargetNode == Entity.Null)
                {
                    Debug.LogWarning(
                        "[WorkerGather] Worker chưa được gán mỏ. Hãy select worker rồi right click vào ResourceNode."
                    );
                    continue;
                }

                if (!nodeLookup.HasComponent(gather.ValueRO.TargetNode))
                {
                    Debug.LogWarning(
                        $"[WorkerGather] TargetNode {gather.ValueRO.TargetNode} không có ResourceNodeData"
                    );

                    gather.ValueRW.TargetNode = Entity.Null;
                    continue;
                }

                if (nodeLookup[gather.ValueRO.TargetNode].Amount <= 0)
                {
                    Debug.LogWarning("[WorkerGather] Mỏ đã hết tài nguyên.");
                    gather.ValueRW.TargetNode = Entity.Null;
                    continue;
                }
            }

            bool needFindDepot =
                gather.ValueRO.TargetDepot == Entity.Null ||
                !transformLookup.HasComponent(gather.ValueRO.TargetDepot);

            if (needFindDepot)
            {
                Debug.Log("[WorkerGather] Cần tìm ResourceDepot...");

                gather.ValueRW.TargetDepot =
                    FindNearestDepot(workerPos, ref state);

                if (gather.ValueRW.TargetDepot == Entity.Null)
                {
                    Debug.LogWarning("[WorkerGather] KHÔNG tìm thấy ResourceDepot nào");
                    continue;
                }

                Debug.Log(
                    $"[WorkerGather] Tìm thấy ResourceDepot: {gather.ValueRW.TargetDepot}"
                );
            }

            Entity nodeEntity = gather.ValueRO.TargetNode;
            Entity depotEntity = gather.ValueRO.TargetDepot;

            if (!transformLookup.HasComponent(nodeEntity))
            {
                Debug.LogError(
                    $"[WorkerGather] ResourceNode {nodeEntity} không có LocalTransform"
                );
                continue;
            }

            if (!transformLookup.HasComponent(depotEntity))
            {
                Debug.LogError(
                    $"[WorkerGather] Depot {depotEntity} không có LocalTransform"
                );
                continue;
            }

            float3 nodePos = transformLookup[nodeEntity].Position;
            float3 depotPos = transformLookup[depotEntity].Position;

            Debug.Log($"[WorkerGather] NodePos={nodePos} DepotPos={depotPos}");

            switch (gather.ValueRO.State)
            {
                case WorkerGatherState.GoingToNode:
                    {
                        Debug.Log("[WorkerGather] State = GoingToNode");

                        bool moveEnabled =
                            SystemAPI.IsComponentEnabled<MoveOverride>(workerEntity);

                        Debug.Log(
                            $"[WorkerGather] MoveOverride enabled = {moveEnabled}"
                        );

                        if (!moveEnabled)
                        {
                            Debug.Log($"[WorkerGather] MoveTo Node {nodePos}");

                            MoveTo(
                                ecb,
                                workerEntity,
                                nodePos,
                                gather.ValueRO.StopDistanceSq
                            );
                        }

                        float distSq =
                            math.distancesq(workerPos, nodePos);

                        Debug.Log(
                            $"[WorkerGather] DistanceSq to node = {distSq}, StopDistanceSq = {gather.ValueRO.StopDistanceSq}"
                        );

                        if (distSq <= gather.ValueRO.StopDistanceSq)
                        {
                            Debug.Log(
                                "[WorkerGather] Đã tới mỏ → chuyển sang Gathering"
                            );

                            ecb.SetComponentEnabled<MoveOverride>(
                                workerEntity,
                                false
                            );

                            gather.ValueRW.State =
                                WorkerGatherState.Gathering;

                            gather.ValueRW.GatherTimer =
                                gather.ValueRO.GatherTime;
                        }

                        break;
                    }

                case WorkerGatherState.Gathering:
                    {
                        Debug.Log(
                            $"[WorkerGather] State = Gathering Timer={gather.ValueRO.GatherTimer}"
                        );

                        gather.ValueRW.GatherTimer -= dt;

                        if (gather.ValueRO.GatherTimer > 0f)
                            break;

                        ResourceNodeData node =
                            nodeLookup[nodeEntity];

                        int amount =
                            math.min(
                                gather.ValueRO.Capacity,
                                node.Amount
                            );

                        Debug.Log(
                            $"[WorkerGather] Gather done. Type={node.Type}, AmountTaken={amount}, NodeRemainBefore={node.Amount}"
                        );

                        node.Amount -= amount;
                        nodeLookup[nodeEntity] = node;

                        gather.ValueRW.CarryAmount = amount;
                        gather.ValueRW.CurrentResourceType = node.Type;

                        gather.ValueRW.State =
                            WorkerGatherState.ReturningDepot;

                        ecb.SetComponentEnabled<MoveOverride>(
                            workerEntity,
                            false
                        );

                        break;
                    }

                case WorkerGatherState.ReturningDepot:
                    {
                        Debug.Log("[WorkerGather] State = ReturningDepot");

                        bool moveEnabled =
                            SystemAPI.IsComponentEnabled<MoveOverride>(workerEntity);

                        Debug.Log(
                            $"[WorkerGather] MoveOverride enabled = {moveEnabled}"
                        );

                        if (!moveEnabled)
                        {
                            Debug.Log($"[WorkerGather] MoveTo Depot {depotPos}");

                            MoveTo(
                                ecb,
                                workerEntity,
                                depotPos,
                                gather.ValueRO.StopDistanceSq
                            );
                        }

                        float distSq =
                            math.distancesq(workerPos, depotPos);

                        Debug.Log(
                            $"[WorkerGather] DistanceSq to depot = {distSq}, StopDistanceSq = {gather.ValueRO.StopDistanceSq}"
                        );

                        if (distSq <= gather.ValueRO.StopDistanceSq)
                        {
                            Debug.Log(
                                $"[WorkerGather] Đã về depot → cộng {gather.ValueRO.CarryAmount} {gather.ValueRO.CurrentResourceType}"
                            );

                            ecb.SetComponentEnabled<MoveOverride>(
                                workerEntity,
                                false
                            );

                            AddResource(
                                ref playerResource.ValueRW,
                                gather.ValueRO.CurrentResourceType,
                                gather.ValueRO.CarryAmount
                            );

                            gather.ValueRW.CarryAmount = 0;

                            gather.ValueRW.State =
                                WorkerGatherState.GoingToNode;
                        }

                        break;
                    }
            }
        }

        if (workerCount == 0)
        {
            Debug.LogWarning(
                "[WorkerGather] Không có worker nào match query WorkerTag + WorkerGatherData"
            );
        }
    }

    private static void MoveTo(
        EntityCommandBuffer ecb,
        Entity entity,
        float3 target,
        float stopDistanceSq)
    {
        Debug.Log(
            $"[WorkerGather] Set MoveOverride target={target}"
        );

        ecb.SetComponent(entity, new MoveOverride
        {
            targetPosition = target,
            targetApplied = false
        });

        ecb.SetComponentEnabled<MoveOverride>(entity, true);
    }

    private static void AddResource(
        ref PlayerResourceData res,
        ResourceType type,
        int amount)
    {
        switch (type)
        {
            case ResourceType.Gold:
                res.Gold += amount;
                break;

            case ResourceType.Wood:
                res.Wood += amount;
                break;

            case ResourceType.Food:
                res.Food += amount;
                break;
        }

        Debug.Log(
            $"[WorkerGather] PlayerResource = Gold:{res.Gold}, Wood:{res.Wood}, Food:{res.Food}"
        );
    }

    private Entity FindNearestResourceNode(
        float3 workerPos,
        ref SystemState state)
    {
        Debug.Log("[WorkerGather] FindNearestResourceNode START");

        Entity nearest = Entity.Null;
        float bestDistSq = float.MaxValue;
        int count = 0;

        foreach (var (node, transform, entity) in
                 SystemAPI.Query<
                         RefRO<ResourceNodeData>,
                         RefRO<LocalTransform>>()
                     .WithEntityAccess())
        {
            count++;

            Debug.Log(
                $"[WorkerGather] Node found Entity={entity}, Type={node.ValueRO.Type}, Amount={node.ValueRO.Amount}, Pos={transform.ValueRO.Position}"
            );

            if (node.ValueRO.Amount <= 0)
            {
                Debug.Log(
                    "[WorkerGather] Skip node vì Amount <= 0"
                );

                continue;
            }

            float distSq =
                math.distancesq(
                    workerPos,
                    transform.ValueRO.Position
                );

            Debug.Log(
                $"[WorkerGather] Node distSq = {distSq}"
            );

            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                nearest = entity;
            }
        }

        Debug.Log(
            $"[WorkerGather] FindNearestResourceNode END. Count={count}, Nearest={nearest}, BestDistSq={bestDistSq}"
        );

        return nearest;
    }

    private Entity FindNearestDepot(
        float3 workerPos,
        ref SystemState state)
    {
        Debug.Log("[WorkerGather] FindNearestDepot START");

        Entity nearest = Entity.Null;
        float bestDistSq = float.MaxValue;
        int count = 0;

        foreach (var (transform, entity) in
                 SystemAPI.Query<RefRO<LocalTransform>>()
                     .WithAll<ResourceDepotTag>()
                     .WithNone<UnderConstructionTag>()
                     .WithEntityAccess())
        {
            count++;

            Debug.Log(
                $"[WorkerGather] Depot found Entity={entity}, Pos={transform.ValueRO.Position}"
            );

            float distSq =
                math.distancesq(
                    workerPos,
                    transform.ValueRO.Position
                );

            Debug.Log(
                $"[WorkerGather] Depot distSq = {distSq}"
            );

            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                nearest = entity;
            }
        }

        Debug.Log(
            $"[WorkerGather] FindNearestDepot END. Count={count}, Nearest={nearest}, BestDistSq={bestDistSq}"
        );

        return nearest;
    }
}