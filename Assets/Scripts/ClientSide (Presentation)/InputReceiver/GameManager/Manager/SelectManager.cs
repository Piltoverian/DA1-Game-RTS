using gameManagerModule;
using NUnit.Framework;
using System.Collections.Generic;
using System.Drawing;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.LowLevelPhysics2D;
public class SelectManager : MonoBehaviour, IFixedUpdateModule
{
    [SerializeField] private StartEndRect selectingRect;
    float holdbuffer = 0;
    bool addbuffer = false;
    Camera cam = null;  
    public void AwakeModule()
    {
        cam=Camera.main;
    }
    public void OnGameStart()
    {
        
    }

    // Update is called once per frame
    public void FixedUpdateModule()
    {
        if (addbuffer)
        {
            holdbuffer += Time.fixedDeltaTime;
        }
        var worldECS = World.DefaultGameObjectInjectionWorld;
        var em = worldECS.EntityManager;
        Vector2 MousePos = Mouse.current.position.ReadValue();
        if (GameManager.Instance.GetModule<FixedUpdateInputTracker>().IsJustPress(Mouse.current.leftButton))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                Debug.Log("Click blocked by UI");
                return;
            }
            SingleSelecting(MousePos, em);
            return;
        }
        else if (GameManager.Instance.GetModule<FixedUpdateInputTracker>().IsHolding(Mouse.current.leftButton) && holdbuffer > 0.05)
        {
            if (!selectingRect.isNotNull)
            {
                selectingRect = new StartEndRect(MousePos);
            }
            else
            {
                selectingRect.ExpandTo(MousePos);
                DragSelect(MousePos, em);
            }
            return;
        }
        else if (GameManager.Instance.GetModule<FixedUpdateInputTracker>().IsHolding(Mouse.current.leftButton))
        {
            addbuffer = true;
        }
        else
        {
            addbuffer = false;
            holdbuffer = 0;
            selectingRect.DeleteRect();
        }

        if (GameManager.Instance.GetModule<FixedUpdateInputTracker>().IsJustPress(Mouse.current.rightButton))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                Debug.Log("Click blocked by UI");
                return;
            }
            else
            {
                var selectedEntities = SelectHelper.GetAllSelectedEntities();
                for (int i = 0; i < selectedEntities.Count; i++)
                {
                    var entity = selectedEntities[i];
                    if (!em.HasComponent<Unit>(entity))
                    {
                        continue;
                    }
                    else
                    {
                        if (TryCommandGather(em))
                        {
                            continue;
                        }
                        CommandMove(em);
                    }
                    
                }
            }
        }
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
    public void SingleSelecting(Vector2 currentMousePos,EntityManager em)
    {
        if (cam==null)
        {
            Debug.Log("WhereMyCam Wth", cam);
            return;
        }
        
        if (em != null)
        {
            Entity selectmanagerentity = em.CreateEntityQuery(typeof(DOTSSelectManagerComponent)).GetSingletonEntity();
            em.AddComponentData(selectmanagerentity, new SelectionRequest
            {
                mode = SelectionMode.Click,
                playerId = 1,
                targetpos = PhysicConvertHelper.ConvertScreenToWorldPos(currentMousePos,cam),
                v1 = PhysicConvertHelper.ConvertScreenToWorldPos(selectingRect.MinPoint,cam),
                v2 = PhysicConvertHelper.ConvertScreenToWorldPos(selectingRect.MaxPoint, cam),
                v3 = PhysicConvertHelper.ConvertScreenToWorldPos(new float2(selectingRect.MinPoint.x, selectingRect.MaxPoint.y), cam),
                v4 = PhysicConvertHelper.ConvertScreenToWorldPos(new float2(selectingRect.MaxPoint.x, selectingRect.MinPoint.y), cam),
                rayInput = PhysicConvertHelper.GetRayCastInput(currentMousePos, cam, uint.MaxValue)//uint.MaxValue
            });
        }

    }

    public void DragSelect(Vector2 currentMousePos, EntityManager em)
    {
      

        if (em != null)
        {
            Entity selectmanagerentity = em.CreateEntityQuery(typeof(DOTSSelectManagerComponent)).GetSingletonEntity();
            em.AddComponentData(selectmanagerentity, new SelectionRequest
            {
                mode = SelectionMode.Drag,
                playerId = 1,
                targetpos = PhysicConvertHelper.ConvertScreenToWorldPos(currentMousePos, cam),
                v1 = PhysicConvertHelper.ConvertScreenToWorldPos(selectingRect.MinPoint, cam),
                v2 = PhysicConvertHelper.ConvertScreenToWorldPos(selectingRect.MaxPoint, cam),
                v3 = PhysicConvertHelper.ConvertScreenToWorldPos(new float2(selectingRect.MinPoint.x, selectingRect.MaxPoint.y), cam),
                v4 =PhysicConvertHelper.ConvertScreenToWorldPos(new float2(selectingRect.MaxPoint.x, selectingRect.MinPoint.y), cam)
            });
        }
    }

    public StartEndRect GetCurrentSelectionRect()
    {
        return selectingRect;
    }

    private bool TryCommandGather(EntityManager entityManager)
    {
        UnityEngine.Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out UnityEngine.RaycastHit hit, 500f))
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

}

public struct StartEndRect
{
    public float2 StartPoint;
    public float2 EndPoint;
    public float2 MinPoint;
    public float2 MaxPoint;
    public bool isNotNull;

    public StartEndRect(float2 mousePos)
    {
        StartPoint = mousePos;
        EndPoint = mousePos;
        isNotNull = true;
        MinPoint = mousePos;
        MaxPoint = mousePos;
    }

    public void ExpandTo(float2 point)
    {
        EndPoint = point;
        MinPoint=math.min(StartPoint, EndPoint);
        MaxPoint=math.max(StartPoint, EndPoint);
    }
    public bool isContains(float2 point)
    {
        if (!Inrange(MinPoint.x, point.x, MaxPoint.x))
        {
            return false;
        }
        if (!Inrange(MinPoint.y, point.y, MaxPoint.y))
        {
            return false;
        }
        return true;
    }

    public bool Inrange(float min, float current, float max)
    {
        if (min > current)
        {
            return false;
        }
        if (max < current)
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    public void DeleteRect()
    {
        isNotNull = false;
        StartPoint=default(float2);
        EndPoint=default(float2);
    }
}
public static class PhysicConvertHelper
{
    public static RaycastInput GetRayCastInput(Vector2 screenPos,Camera cam, uint layerMaskFilter)
    {
        UnityEngine.Ray ray = cam.ScreenPointToRay(screenPos);
        float3 start = ray.origin;
        float3 end = ray.origin + ray.direction * 1000f;
        RaycastInput raycastInput = new RaycastInput
        {
            Start = start,
            End = end,
            Filter = new CollisionFilter
            {
                BelongsTo = uint.MaxValue,
                CollidesWith = layerMaskFilter
            }
        };

        return raycastInput;
    }

    public static float3 ConvertScreenToWorldPos(Vector2 screenPos,Camera cam)
    {
        RaycastInput input = GetRayCastInput(screenPos,cam,PhysicsLayersDefine.Ground);
        var world = World.DefaultGameObjectInjectionWorld;
        var entityManager = world.EntityManager;

        var physicsWorld = entityManager.CreateEntityQuery(typeof(PhysicsWorldSingleton))
                                        .GetSingleton<PhysicsWorldSingleton>();
        if (physicsWorld.CastRay(input, out Unity.Physics.RaycastHit hit))
        {
            return hit.Position;
        }
        return default;
    }
}

public static class SelectHelper
{
    public static Entity GetFirstSelectedEntity()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        var entityManager = world.EntityManager;
        var query = entityManager.CreateEntityQuery(typeof(Selected)).ToEntityArray(Unity.Collections.Allocator.Temp);
        if (query.Length > 0)
        {
            return query[0];
        }
        return Entity.Null;
    }

    public static List<Entity> GetAllSelectedEntities()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        var entityManager = world.EntityManager;
        var query = entityManager.CreateEntityQuery(typeof(Selected)).ToEntityArray(Unity.Collections.Allocator.Temp);
        List<Entity> selectedEntities = new List<Entity>(query);
        return selectedEntities;
    }



}