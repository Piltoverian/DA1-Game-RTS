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


        var ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        int workerCount = 0;

        foreach (var (workerTransform,unit,gather, workerEntity) in
                 SystemAPI.Query<RefRO<LocalTransform>,RefRO<Unit>,RefRW<WorkerGatherData>>()
                     .WithAll<WorkerTag>()
                     .WithEntityAccess())
        {
            workerCount++;

            float3 workerPos = workerTransform.ValueRO.Position;
            float2 WorkerPos2D = workerPos.xz;
            bool needFindNode =
                gather.ValueRO.TargetNode == Entity.Null ||
                !nodeLookup.HasComponent(gather.ValueRO.TargetNode) ||
                nodeLookup[gather.ValueRO.TargetNode].Amount <= 0;

            if (needFindNode)
            {
                if (gather.ValueRO.TargetNode == Entity.Null)
                {
                    continue;
                }

                if (!nodeLookup.HasComponent(gather.ValueRO.TargetNode))
                {
                    gather.ValueRW.TargetNode = Entity.Null;
                    continue;
                }

                if (nodeLookup[gather.ValueRO.TargetNode].Amount <= 0)
                {
                    gather.ValueRW.TargetNode = Entity.Null;
                    continue;
                }
            }

            bool needFindDepot =
                gather.ValueRO.TargetDepot == Entity.Null ||
                !transformLookup.HasComponent(gather.ValueRO.TargetDepot);

            if (needFindDepot)
            {
                gather.ValueRW.TargetDepot =
                    FindNearestDepot(workerPos, ref state);

                if (gather.ValueRW.TargetDepot == Entity.Null)
                {
                    continue;
                }
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
            float2 nodePos2D = nodePos.xz;
            float3 depotPos = transformLookup[depotEntity].Position;
            float2 depotPos2D = depotPos.xz;

            switch (gather.ValueRO.State)
            {
                case WorkerGatherState.GoingToNode:
                    {
                        bool moveEnabled =
                            SystemAPI.IsComponentEnabled<MoveOverride>(workerEntity);

                        if (!moveEnabled)
                        {
                            MoveTo(
                                ecb,
                                workerEntity,
                                nodePos,
                                gather.ValueRO.StopDistanceSq
                            );
                        }

                        float distSq =
                            math.distancesq(WorkerPos2D, nodePos2D);

                        float interactDist = math.sqrt(gather.ValueRO.StopDistanceSq);
                        if (SystemAPI.HasComponent<BuildingData>(nodeEntity)) {
                            var bData = SystemAPI.GetComponent<BuildingData>(nodeEntity);
                            interactDist += math.max(bData.FootprintSizeX, bData.FootprintSizeZ) * 0.5f;
                        } else {
                            interactDist += 1.5f; // Bù hao cho Resource Node
                        }

                        bool isSettled = SystemAPI.GetComponent<MovementSteeringComponent>(workerEntity).isSettled;

                        if (distSq <= interactDist * interactDist || (!moveEnabled && isSettled && distSq <= interactDist * interactDist * 1.5f))
                        {
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
                        bool moveEnabled =
                            SystemAPI.IsComponentEnabled<MoveOverride>(workerEntity);

                        if (!moveEnabled)
                        {
                            MoveTo(
                                ecb,
                                workerEntity,
                                depotPos,
                                gather.ValueRO.StopDistanceSq
                            );
                        }

                        float distSq =
                            math.distancesq(WorkerPos2D, depotPos2D);

                        float interactDist = math.sqrt(gather.ValueRO.StopDistanceSq);
                        if (SystemAPI.HasComponent<BuildingData>(depotEntity)) {
                            var bData = SystemAPI.GetComponent<BuildingData>(depotEntity);
                            interactDist += math.max(bData.FootprintSizeX, bData.FootprintSizeZ) * 0.5f;
                        } else {
                            interactDist += 2.0f; // Bù hao cho Depot
                        }

                        bool isSettled = SystemAPI.GetComponent<MovementSteeringComponent>(workerEntity).isSettled;

                        if (distSq <= interactDist * interactDist || (!moveEnabled && isSettled && distSq <= interactDist * interactDist * 1.5f))
                        {
                            ecb.SetComponentEnabled<MoveOverride>(
                                workerEntity,
                                false
                            );


                            float currentAmount;
                            if (PlayerContextHelper.GetPlayerResourceByType(state.EntityManager, unit.ValueRO.playerID, gather.ValueRO.CurrentResourceType, out currentAmount)==FunctionResult.Success)
                            {
                                PlayerContextHelper.SetPlayerResource(state.EntityManager, unit.ValueRO.playerID, gather.ValueRO.CurrentResourceType, currentAmount + gather.ValueRO.CarryAmount);
                            }
                            else
                            {
                                Debug.LogError($"[WorkerGather] Không thể lấy resource của player {unit.ValueRO.playerID} loại {gather.ValueRO.CurrentResourceType}");
                            }

                            gather.ValueRW.CarryAmount = 0;

                            gather.ValueRW.State =
                                WorkerGatherState.GoingToNode;
                        }

                        break;
                    }
            }
        }
    }

    private static void MoveTo(
        EntityCommandBuffer ecb,
        Entity entity,
        float3 target,
        float stopDistanceSq)
    {
        ecb.SetComponent(entity, new MoveOverride
        {
            targetPosition = target,
            targetApplied = false
        });

        ecb.SetComponentEnabled<MoveOverride>(entity, true);
    }

    private Entity FindNearestResourceNode(
        float3 workerPos,
        ref SystemState state)
    {
        Entity nearest = Entity.Null;
        float bestDistSq = float.MaxValue;

        foreach (var (node, transform, entity) in
                 SystemAPI.Query<
                         RefRO<ResourceNodeData>,
                         RefRO<LocalTransform>>()
                     .WithEntityAccess())
        {
            if (node.ValueRO.Amount <= 0)
            {
                continue;
            }

            float distSq =
                math.distancesq(
                    workerPos,
                    transform.ValueRO.Position
                );

            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                nearest = entity;
            }
        }

        return nearest;
    }

    private Entity FindNearestDepot(
        float3 workerPos,
        ref SystemState state)
    {
        Entity nearest = Entity.Null;
        float bestDistSq = float.MaxValue;

        foreach (var (transform, entity) in
                 SystemAPI.Query<RefRO<LocalTransform>>()
                     .WithAll<ResourceDepotTag>()
                     .WithNone<UnderConstructionTag>()
                     .WithEntityAccess())
        {
            float distSq =
                math.distancesq(
                    workerPos,
                    transform.ValueRO.Position
                );

            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                nearest = entity;
            }
        }

        return nearest;
    }
}