using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.LowLevelPhysics2D;
using UnityEngine.Windows;

public class UnitController : MonoBehaviour
{
    public static UnitController Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.deleteKey.wasPressedThisFrame)
        {
            TryCancelSelectedBuildings();
        }

        if (!GameManager.Instance.GetModule<FixedUpdateInputTracker>().IsJustPress(Mouse.current.rightButton))
            return;
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        if (TryCommandAttack(entityManager))
        {
            return;
        }

        if (TryCommandGather(entityManager))
        {
            return;
        }

        if (TryCommandBuild(entityManager))
        {
            return;
        }

        CommandMove(entityManager);
    }

    private void TryCancelSelectedBuildings()
    {
        int playerId = GetCurrentPlayerId();
        if (playerId < 0)
            return;

        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null)
            return;

        var entityManager = world.EntityManager;
        var selectedEntities = SelectHelper.GetAllSelectedEntitiesByplayerID(playerId);
        if (selectedEntities == null || selectedEntities.Count == 0)
            return;

        foreach (Entity entity in selectedEntities)
        {
            if (entityManager.Exists(entity) && entityManager.HasComponent<BuildingStateComponent>(entity))
            {
                Entity req = entityManager.CreateEntity();
                entityManager.AddComponentData(req, new CancelBuildingRequest
                {
                    BuildingEntity = entity,
                    PlayerId = playerId
                });
            }
        }
    }

    private bool TryCommandGather(EntityManager entityManager)
    {
        int playerId = GetCurrentPlayerId();
        if (playerId < 0)
            return false;

        UnityEngine.Ray ray = Camera.main.ScreenPointToRay(UnityEngine.Input.mousePosition);
        if (!Physics.Raycast(ray, out UnityEngine.RaycastHit hit, 500f))
            return false;

        if (hit.collider.GetComponentInParent<ResourceNodeReference>() == null)
            return false;

        Entity resourceNode = FindResourceNodeEntityNearHit(entityManager, hit.point);
        if (resourceNode == Entity.Null)
        {
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
                playerId,
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
            float3 centerPos = entityManager.HasComponent<BlockageData>(node)
                ? entityManager.GetComponentData<BlockageData>(node).GetWorldCenter(transform.Position)
                : transform.Position;

            float distSq = Vector3.SqrMagnitude((Vector3)centerPos - hitPoint);

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
                playerId,
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
    }

    private bool TryCommandAttack(EntityManager entityManager)
    {
        var physicalQuery = entityManager.CreateEntityQuery(typeof(PhysicsWorldSingleton));
        if (physicalQuery.IsEmpty)
        {
            return false;
        }

        PhysicsWorldSingleton physicsWorld = physicalQuery.GetSingleton<PhysicsWorldSingleton>();

        UnityEngine.Ray ray = Camera.main.ScreenPointToRay(UnityEngine.Input.mousePosition);

        RaycastInput input = new RaycastInput
        {
            Start = ray.origin,
            End = ray.origin + ray.direction * 2000f,
            Filter = CollisionFilter.Default
        };

        if (!physicsWorld.CastRay(input, out Unity.Physics.RaycastHit hit))
            return false;

        Entity hitEntity = hit.Entity;

        int myPlayerId = GetCurrentPlayerId();
        
        if (!entityManager.HasComponent<Unit>(hitEntity))
        {
            return false;
        }
        
        int hitEntityPlayerId = entityManager.GetComponentData<Unit>(hitEntity).playerID;
        if (myPlayerId==hitEntityPlayerId)
            return false;
        var selectedEntities = SelectHelper.GetAllSelectedEntitiesByplayerID(myPlayerId);
        if (selectedEntities.Count == 0)
            return false;
        int attackCommandedCount = 0;
        foreach (Entity unit in selectedEntities)
        {
            if (!entityManager.HasComponent<ShootAttack>(unit) ||
                !entityManager.HasComponent<Target>(unit))
                continue;
            CommandDataHelper.AddCommandToQueue(
                entityManager,
                myPlayerId,
                unit,
                new CommandData
                {
                    Type = CommandType.TargetTo,
                    indexInUnitCommandList = 0
                },
                targetEntity: hitEntity
            );
            attackCommandedCount++;
        }
        return attackCommandedCount > 0;
    }

    private bool TryCommandBuild(EntityManager entityManager)
    {
        int playerId = GetCurrentPlayerId();
        if (playerId < 0)
            return false;

        UnityEngine.Ray ray = Camera.main.ScreenPointToRay(UnityEngine.Input.mousePosition);

        Entity targetBuilding = FindUnderConstructionBuildingNearHit(entityManager, ray);
        if (targetBuilding == Entity.Null || !BuildingHelper.CanBuildOrRepair(entityManager, targetBuilding))
        {
            return false;
        }

        if (entityManager.HasComponent<Unit>(targetBuilding))
        {
            if (entityManager.GetComponentData<Unit>(targetBuilding).playerID != playerId)
            {
                return false;
            }
        }

        var selectedEntities = SelectHelper.GetAllSelectedEntitiesByplayerID(playerId);
        if (selectedEntities.Count == 0)
            return false;

        int queuedCount = 0;
        foreach (Entity worker in selectedEntities)
        {
            if (!entityManager.HasComponent<BuilderComponent>(worker) ||
                !entityManager.HasComponent<MoveOverride>(worker))
            {
                continue;
            }

            CommandDataHelper.AddCommandToQueue(
                entityManager,
                playerId,
                worker,
                new CommandData
                {
                    Type = CommandType.Build,
                    indexInUnitCommandList = 0
                },
                targetEntity: targetBuilding
            );

            queuedCount++;
        }

        return queuedCount > 0;
    }

    private Entity FindUnderConstructionBuildingNearHit(EntityManager entityManager, UnityEngine.Ray ray)
    {
        var physicalQuery = entityManager.CreateEntityQuery(typeof(PhysicsWorldSingleton));
        if (!physicalQuery.IsEmpty)
        {
            PhysicsWorldSingleton physicsWorld = physicalQuery.GetSingleton<PhysicsWorldSingleton>();
            RaycastInput input = new RaycastInput
            {
                Start = ray.origin,
                End = ray.origin + ray.direction * 2000f,
                Filter = CollisionFilter.Default
            };

            if (physicsWorld.CastRay(input, out Unity.Physics.RaycastHit hit))
            {
                Entity hitEntity = hit.Entity;
                if (BuildingHelper.CanBuildOrRepair(entityManager, hitEntity))
                {
                    return hitEntity;
                }
            }
        }

        if (UnityEngine.Physics.Raycast(ray, out UnityEngine.RaycastHit groundHit, 500f))
        {
            Vector3 hitPoint = groundHit.point;
            EntityQuery query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<BuildingStateComponent, LocalTransform, BlockageData>()
                .Build(entityManager);

            if (!query.IsEmpty)
            {
                NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
                Entity nearest = Entity.Null;

                for (int i = 0; i < entities.Length; i++)
                {
                    Entity bEntity = entities[i];
                    if (!BuildingHelper.CanBuildOrRepair(entityManager, bEntity))
                    {
                        continue;
                    }

                    LocalTransform trans = entityManager.GetComponentData<LocalTransform>(bEntity);
                    BlockageData blockage = entityManager.GetComponentData<BlockageData>(bEntity);

                    float2 min = trans.Position.xz + blockage.LocalRect.MinPoint;
                    float2 max = trans.Position.xz + blockage.LocalRect.MaxPoint;

                    if (hitPoint.x >= min.x && hitPoint.x <= max.x &&
                        hitPoint.z >= min.y && hitPoint.z <= max.y)
                    {
                        nearest = bEntity;
                        break;
                    }
                }

                entities.Dispose();
                query.Dispose();
                return nearest;
            }
            else
            {
                query.Dispose();
            }
        }

        return Entity.Null;
    }
}
