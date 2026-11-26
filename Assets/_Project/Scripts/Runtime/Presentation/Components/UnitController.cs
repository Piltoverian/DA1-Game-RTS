using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
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

    private static CollisionFilter NonGroundFilter => new CollisionFilter
    {
        BelongsTo = uint.MaxValue,
        CollidesWith = ~PhysicsLayersDefine.Ground
    };

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (!SelectHelper.TryGetLocalPlayerId(out _) || Mouse.current == null) return;
        if (GameplayInputGate.BlocksWorldInput || GameplayInputGate.IsPointerOverUI()) return;
        if (Keyboard.current != null && Keyboard.current.deleteKey.wasPressedThisFrame && !ResourceInspection.TryGet(out _))
        {
            TryCancelSelectedBuildings();
        }

        // Handle this render-frame edge once, rather than a FixedUpdate status that can persist.
        if (!Mouse.current.rightButton.wasPressedThisFrame)
            return;
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (!entityManager.World.IsClient() && FogOrderRules.HasFog(entityManager)
            && FogCommandPicking.Local(World.DefaultGameObjectInjectionWorld, GetCurrentPlayerId()) == null)
        { CommandMove(entityManager); return; }

        if (TryCommandAttack(entityManager))
        {
            return;
        }

        if (TryCommandGather(entityManager, Camera.main, Mouse.current.position.ReadValue()))
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
            if (entityManager.Exists(entity) && entityManager.HasComponent<BuildingConstruction>(entity))
            {
                CommandDataHelper.CancelBuilding(entityManager, playerId, entity);
            }
        }
    }

    // Both pointer inspection and commands must hit the same baked ECS resource.
    public bool TryCommandGather(EntityManager entityManager, Camera camera, Vector2 screenPosition)
    {
        if (camera == null || !entityManager.World.IsCreated) return false;
        int playerId = GetCurrentPlayerId();
        if (playerId < 0)
            return false;

        var fog = FogCommandPicking.Local(entityManager.World, playerId);
        if (entityManager.World.IsClient())
        {
            bool liveResource = TryPickNetworkEntity(entityManager, out var resource) && entityManager.HasComponent<ResourceNodeData>(resource);
            ulong rememberedId = 0;
            if (!liveResource && !NeutralMemoryPresentationSystem.TryPick(entityManager.World,
                camera.ScreenPointToRay(screenPosition), FogRenderKind.Resource, out rememberedId)) return false;
            int sent = 0;
            foreach (var worker in SelectHelper.GetAllSelectedEntitiesByplayerID(playerId))
            {
                if (!entityManager.HasComponent<WorkerGatherData>(worker)) continue;
                if (liveResource) CommandDataHelper.AddCommandToQueue(entityManager, playerId, worker, CommandType.TargetTo, targetEntity: resource);
                else GameplayCommandNetwork.SendKnownNeutral(entityManager, worker, rememberedId);
                sent++;
            }
            return sent > 0;
        }
        if (fog != null)
        {
            if (!FogCommandPicking.TryPick(fog, camera.ScreenPointToRay(screenPosition),
                FogRenderKind.Resource, out var snapshot, out var known)) return false;
            int sent = 0;
            foreach (var worker in SelectHelper.GetAllSelectedEntitiesByplayerID(playerId))
            {
                if (!entityManager.HasComponent<WorkerGatherData>(worker) || !entityManager.HasComponent<MoveOverride>(worker)) continue;
                CommandDataHelper.AddCommandToQueue(entityManager, playerId, worker, CommandType.TargetTo,
                    targetEntity: snapshot.Entity, position: known.Position, knownTarget: known);
                sent++;
            }
            return sent > 0;
        }

        if (!ResourceInspection.TryResolveNode(camera, screenPosition, entityManager, out Entity resourceNode))
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
                CommandType.TargetTo,
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
        return SelectHelper.TryGetLocalPlayerId(out int playerId) ? playerId : -1;
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
                CommandType.Move,
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
            Filter = NonGroundFilter
        };

        if (!physicsWorld.CastRay(input, out Unity.Physics.RaycastHit hit))
            return false;

        Entity hitEntity = hit.Entity;

        int myPlayerId = GetCurrentPlayerId();
        
        if (!entityManager.HasComponent<EntityOwner>(hitEntity))
        {
            return false;
        }
        
        int hitEntityPlayerId = entityManager.GetComponentData<EntityOwner>(hitEntity).PlayerID;
        if (myPlayerId==hitEntityPlayerId)
            return false;
        if (!FogOrderRules.IsVisible(entityManager, myPlayerId, hitEntity)) return false;
        if (PlayerContextHelper.AreAllies(entityManager, myPlayerId, hitEntityPlayerId)) return false;
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
                CommandType.TargetTo,
                targetEntity: hitEntity,
                position: MouseWorldPosition.Instance.GetPosition()
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

        var fog = FogCommandPicking.Local(World.DefaultGameObjectInjectionWorld, playerId);
        if (fog != null)
        {
            if (!FogCommandPicking.TryPick(fog, Camera.main.ScreenPointToRay(UnityEngine.Input.mousePosition),
                FogRenderKind.Building, out var snapshot, out var known) || snapshot.OwnerId != playerId) return false;
            if (snapshot.IsVisible && !BuildingHelper.CanBuildOrRepair(entityManager, snapshot.Entity)) return false;
            bool unfinished = snapshot.ConstructionPhase == ConstructionPhase.Planned || snapshot.ConstructionPhase == ConstructionPhase.UnderConstruction;
            // Even an apparently healthy remembered building may now need repair: inspect only on arrival.
            int sent = 0;
            foreach (var worker in SelectHelper.GetAllSelectedEntitiesByplayerID(playerId))
            {
                if (!entityManager.HasComponent<BuilderComponent>(worker) || !entityManager.HasComponent<MoveOverride>(worker)
                    || !entityManager.HasBuffer<BuildOffer>(worker)) continue;
                bool allowed = false;
                foreach (var offer in entityManager.GetBuffer<BuildOffer>(worker))
                    if (snapshot.GameRegId.HasValue && offer.DefinitionID == snapshot.GameRegId.Value) allowed = true;
                if (!allowed) continue;
                CommandDataHelper.AddCommandToQueue(entityManager, playerId, worker,
                    unfinished ? CommandType.Build : CommandType.TargetTo,
                    targetEntity: snapshot.Entity, position: known.Position, knownTarget: known);
                sent++;
            }
            return sent > 0;
        }

        UnityEngine.Ray ray = Camera.main.ScreenPointToRay(UnityEngine.Input.mousePosition);

        Entity targetBuilding = FindUnderConstructionBuildingNearHit(entityManager, ray);
        if (targetBuilding == Entity.Null || !BuildingHelper.CanBuildOrRepair(entityManager, targetBuilding))
        {
            return false;
        }

        if (entityManager.HasComponent<EntityOwner>(targetBuilding))
        {
            if (entityManager.GetComponentData<EntityOwner>(targetBuilding).PlayerID != playerId)
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
                BuildingHelper.IsUnderConstruction(entityManager, targetBuilding) ? CommandType.Build : CommandType.TargetTo,
                targetEntity: targetBuilding
            );
            queuedCount++;
        }

        return queuedCount > 0;
    }

    private static bool TryPickNetworkEntity(EntityManager em, out Entity entity)
    {
        entity = Entity.Null;
        if (Camera.main == null) return false;
        using var query = em.CreateEntityQuery(typeof(PhysicsWorldSingleton));
        if (!query.TryGetSingleton<PhysicsWorldSingleton>(out var physics)) return false;
        var ray = Camera.main.ScreenPointToRay(UnityEngine.Input.mousePosition);
        var input = new RaycastInput { Start = ray.origin, End = ray.origin + ray.direction * 2000f, Filter = NonGroundFilter };
        if (!physics.CastRay(input, out Unity.Physics.RaycastHit hit)) return false;
        entity = hit.Entity;
        for (int i = 0; i < 16 && em.Exists(entity) && !em.HasComponent<GhostInstance>(entity) && em.HasComponent<Parent>(entity); i++)
            entity = em.GetComponentData<Parent>(entity).Value;
        return em.Exists(entity) && em.HasComponent<GhostInstance>(entity);
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
                Filter = NonGroundFilter
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
                .WithAll<BuildingConstruction, LocalTransform, BlockageData>()
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
