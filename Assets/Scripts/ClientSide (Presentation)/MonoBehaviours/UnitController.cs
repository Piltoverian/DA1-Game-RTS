using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

public class UnitController : MonoBehaviour
{
    public static UnitController Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (!GameManager.Instance.GetModule<FixedUpdateInputTracker>().IsJustPress(Mouse.current.rightButton))
            return;
        Debug.Log("Right click detected, processing command...");
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        if (TryCommandGather(entityManager))
        {
            Debug.Log("Gather command processed.");
            return;
        }
        CommandMove(entityManager);
    }

    private bool TryCommandGather(EntityManager entityManager)
    {
        int playerId = GetCurrentPlayerId();
        if (playerId < 0)
            return false;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 500f))
            return false;

        if (hit.collider.GetComponentInParent<ResourceNodeReference>() == null)
            return false;

        Entity resourceNode = FindResourceNodeEntityNearHit(entityManager, hit.point);
        if (resourceNode == Entity.Null)
        {
            Debug.LogWarning("Clicked resource object but could not find ECS ResourceNode entity.");
            return false;
        }

        var selectedEntities = SelectHelper.GetAllSelectedEntitiesByplayerID(playerId);
        if (selectedEntities.Count == 0)
            return false;

        int queuedCount = 0;
        foreach (Entity worker in selectedEntities)
        {
            if (!entityManager.HasComponent<WorkerTag>(worker) ||
                !entityManager.HasComponent<WorkerGatherData>(worker) ||
                !entityManager.HasComponent<MoveOverride>(worker))
            {
                continue;
            }

            CommandDataHelper.AddCommandToQueue(
                entityManager,
                worker,
                new CommandData
                {
                    Type = CommandType.TargetTo,
                    indexInUnitCommandList = 0
                },
                targetEntity: resourceNode
            );

            queuedCount++;
        }

        if (queuedCount == 0)
            return false;

        Debug.Log($"Queued gather for {queuedCount} worker(s) on node {resourceNode}");
        return true;
    }

    private int GetCurrentPlayerId()
    {
        var selectManager = GameManager.Instance.GetModule<SelectManager>();
        return selectManager.currentContext.playerId;
    }

    private Entity FindResourceNodeEntityNearHit(EntityManager entityManager, Vector3 hitPoint)
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

        NativeArray<Entity> nodes = nodeQuery.ToEntityArray(Allocator.Temp);

        Entity nearest = Entity.Null;
        float bestDistSq = float.MaxValue;

        for (int i = 0; i < nodes.Length; i++)
        {
            Entity node = nodes[i];
            LocalTransform transform = entityManager.GetComponentData<LocalTransform>(node);
            float distSq = Vector3.SqrMagnitude((Vector3)transform.Position - hitPoint);

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
        int playerId = GetCurrentPlayerId();
        Debug.Log($"Processing move command for player ID: {playerId}");
        if (playerId < 0)
            return;

        Vector3 mouseWorldPosition = MouseWorldPosition.Instance.GetPosition();
        var selectedEntities = SelectHelper.GetAllSelectedEntitiesByplayerID(playerId);
        if (selectedEntities.Count == 0)
            return;

        int queuedCount = 0;
        foreach (Entity entity in selectedEntities)
        {
            if (!entityManager.HasComponent<MoveOverride>(entity))
                continue;

            CommandDataHelper.AddCommandToQueue(
                entityManager,
                entity,
                new CommandData
                {
                    Type = CommandType.Move,
                    indexInUnitCommandList = 0
                },
                position: mouseWorldPosition
            );

            queuedCount++;
        }

        if (queuedCount > 0)
            Debug.Log($"Queued move for {queuedCount} unit(s) to {mouseWorldPosition}");
    }
}
