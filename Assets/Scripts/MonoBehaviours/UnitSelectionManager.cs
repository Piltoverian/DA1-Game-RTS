using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class UnitSelectionManager : MonoBehaviour
{
    public static UnitSelectionManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(1))
            return;

        EntityManager entityManager =
            World.DefaultGameObjectInjectionWorld.EntityManager;

        // 1. Nếu click vào resource node thì cho worker gather
        if (TryCommandGather(entityManager))
            return;

        // 2. Nếu không click resource thì move bình thường
        CommandMove(entityManager);
    }

    private bool TryCommandGather(EntityManager entityManager)
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, 500f))
            return false;

        ResourceNodeReference nodeRef =
            hit.collider.GetComponentInParent<ResourceNodeReference>();

        if (nodeRef == null)
            return false;

        Entity resourceNode =
            FindResourceNodeEntityNearHit(entityManager, hit.point);

        if (resourceNode == Entity.Null)
        {
            Debug.LogWarning("Clicked resource object but could not find ECS ResourceNode entity.");
            return false;
        }

        EntityQuery selectedWorkerQuery =
            new EntityQueryBuilder(Allocator.Temp)
                .WithAll<WorkerTag, WorkerGatherData, Selected>()
                .WithPresent<MoveOverride>()
                .Build(entityManager);

        if (selectedWorkerQuery.IsEmpty)
        {
            selectedWorkerQuery.Dispose();
            return false;
        }

        NativeArray<Entity> workers =
            selectedWorkerQuery.ToEntityArray(Allocator.Temp);

        EntityCommandBuffer ecb =
            new EntityCommandBuffer(Allocator.Temp);

        for (int i = 0; i < workers.Length; i++)
        {
            Entity worker = workers[i];

            WorkerGatherData gather =
                entityManager.GetComponentData<WorkerGatherData>(worker);

            gather.TargetNode = resourceNode;
            gather.TargetDepot = Entity.Null;
            gather.CarryAmount = 0;
            gather.GatherTimer = 0f;
            gather.State = WorkerGatherState.GoingToNode;

            ecb.SetComponent(worker, gather);
            ecb.SetComponentEnabled<MoveOverride>(worker, false);
        }

        int workerCount = workers.Length;

        ecb.Playback(entityManager);
        ecb.Dispose();

        workers.Dispose();
        selectedWorkerQuery.Dispose();

        Debug.Log($"Assigned {workerCount} worker(s) to gather node {resourceNode}");

        return true;
    }
    private Entity FindResourceNodeEntityNearHit(
    EntityManager entityManager,
    Vector3 hitPoint)
    {
        EntityQuery nodeQuery =
            new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ResourceNodeData, ResourceNodeTag, LocalTransform>()
                .Build(entityManager);

        if (nodeQuery.IsEmpty)
        {
            nodeQuery.Dispose();
            return Entity.Null;
        }

        NativeArray<Entity> nodes =
            nodeQuery.ToEntityArray(Allocator.Temp);

        Entity nearest = Entity.Null;
        float bestDistSq = float.MaxValue;

        for (int i = 0; i < nodes.Length; i++)
        {
            Entity node = nodes[i];

            LocalTransform transform =
                entityManager.GetComponentData<LocalTransform>(node);

            float distSq =
                Vector3.SqrMagnitude(
                    (Vector3)transform.Position - hitPoint);

            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                nearest = node;
            }
        }

        nodes.Dispose();
        nodeQuery.Dispose();

        if (bestDistSq > 25f)
            return Entity.Null;

        return nearest;
    }

    private void CommandMove(EntityManager entityManager)
    {
        Vector3 mouseWorldPosition =
            MouseWorldPosition.Instance.GetPosition();

        EntityQuery gridQuery =
            entityManager.CreateEntityQuery(typeof(GridComponent));

        if (gridQuery.IsEmpty)
        {
            Debug.LogWarning("GridComponent not found in world!");
            return;
        }

        Entity gridEntity = gridQuery.GetSingletonEntity();

        GridComponent gridComponent =
            entityManager.GetComponentData<GridComponent>(gridEntity);

        if (gridComponent.width <= 0 || gridComponent.height <= 0)
            return;

        if (!entityManager.HasBuffer<GridNodeCost>(gridEntity) ||
            !entityManager.HasBuffer<GridIsland>(gridEntity))
            return;

        EntityQuery cacheQuery =
            entityManager.CreateEntityQuery(typeof(FlowFieldCache));

        if (cacheQuery.IsEmpty)
            return;

        Entity cacheEntity = cacheQuery.GetSingletonEntity();

        if (!entityManager.HasBuffer<FlowFieldCacheEntry>(cacheEntity))
            return;

        EntityQuery selectedQuery =
            new EntityQueryBuilder(Allocator.Temp)
                .WithAll<MovementAgentComponent, Selected>()
                .WithPresent<MoveOverride>()
                .Build(entityManager);

        if (selectedQuery.IsEmpty)
        {
            selectedQuery.Dispose();
            return;
        }

        NativeArray<Entity> entityArray =
            selectedQuery.ToEntityArray(Allocator.Temp);

        EntityCommandBuffer ecb =
            new EntityCommandBuffer(Allocator.Temp);

        for (int i = 0; i < entityArray.Length; i++)
        {
            Entity entity = entityArray[i];

            MoveOverride moveData =
                entityManager.GetComponentData<MoveOverride>(entity);

            moveData.targetPosition = mouseWorldPosition;
            moveData.targetApplied = false;

            ecb.SetComponent(entity, moveData);
            ecb.SetComponentEnabled<MoveOverride>(entity, true);

            // Nếu unit là worker thì hủy gather khi player ra lệnh move thường
            if (entityManager.HasComponent<WorkerGatherData>(entity))
            {
                WorkerGatherData gather =
                    entityManager.GetComponentData<WorkerGatherData>(entity);

                gather.TargetNode = Entity.Null;
                gather.TargetDepot = Entity.Null;
                gather.CarryAmount = 0;
                gather.State = WorkerGatherState.GoingToNode;

                ecb.SetComponent(entity, gather);
            }
        }

        ecb.Playback(entityManager);
        ecb.Dispose();

        entityArray.Dispose();
        selectedQuery.Dispose();
    }
}