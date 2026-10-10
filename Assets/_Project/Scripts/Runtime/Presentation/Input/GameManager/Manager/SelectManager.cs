using gameManagerModule;

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
    public TestPlayer currentContext{ get; private set; }
    float holdbuffer = 0;
    bool leftHeld;
    bool pressStartedOverUI;
    Camera cam = null;  
    public void AwakeModule()
    {
        cam=Camera.main;
        currentContext = FindAnyObjectByType<TestPlayer>();
    }
    public bool TryGetPlayerId(out int playerId)
    {
        playerId = -1;
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return false;
        using var local = world.EntityManager.CreateEntityQuery(typeof(LocalTestPlayer));
        using var bootstrap = world.EntityManager.CreateEntityQuery(typeof(PlayerBootstrapState));
        int localCount = local.CalculateEntityCount();
        if (localCount > 1) return false;
        if (localCount == 1)
        {
            int bootstrapCount = bootstrap.CalculateEntityCount();
            if (bootstrapCount > 1 || (bootstrapCount == 1 && bootstrap.GetSingleton<PlayerBootstrapState>().Phase != PlayerBootstrapPhase.Ready)) return false;
            int id = local.GetSingleton<LocalTestPlayer>().PlayerId;
            if (PlayerContextHelper.GetContextData(world.EntityManager, id, out _) != FunctionResult.Success) return false;
            playerId = id;
            return true;
        }
        if (currentContext == null) currentContext = FindAnyObjectByType<TestPlayer>();
        return currentContext != null && currentContext.TryGetPlayerId(out playerId);
    }
    public void OnGameStart()
    {
        
    }

    // Update is called once per frame
    public void FixedUpdateModule()
    {
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.isPressed)
        {
            leftHeld = false;
            pressStartedOverUI = false;
            holdbuffer = 0;
            selectingRect.DeleteRect();
            return;
        }
        Vector2 mousePos = mouse.position.ReadValue();
        if (!leftHeld)
        {
            leftHeld = true;
            holdbuffer = 0;
            selectingRect.DeleteRect();
            selectingRect.StartPoint = mousePos;
            pressStartedOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!pressStartedOverUI && TryGetSelectionManager(out var em)) SingleSelecting(mousePos, em);
            return;
        }
        if (pressStartedOverUI) return;
        holdbuffer += Time.fixedDeltaTime;
        if (holdbuffer <= 0.05f) return;
        selectingRect.isNotNull = true;
        selectingRect.ExpandTo(mousePos);
        if (TryGetSelectionManager(out var manager)) DragSelect(mousePos, manager);
    }

    private bool TryGetSelectionManager(out EntityManager entityManager)
    {
        entityManager = default;
        if (!TryGetPlayerId(out _)) return false;
        if (cam == null) cam = Camera.main;
        if (cam == null) return false;
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return false;
        using var selection = world.EntityManager.CreateEntityQuery(typeof(DOTSSelectManagerComponent), typeof(SelectionRequest));
        using var physics = world.EntityManager.CreateEntityQuery(typeof(PhysicsWorldSingleton));
        if (selection.CalculateEntityCount() != 1 || physics.CalculateEntityCount() != 1) return false;
        entityManager = world.EntityManager;
        return true;
    }

    public void SingleSelecting(Vector2 currentMousePos,EntityManager em)
    {
        if (!TryGetPlayerId(out int playerId)) return;
        if (cam==null)
        {
            return;
        }
        
        if (em != null)
        {
            Entity selectmanagerentity = em.CreateEntityQuery(typeof(DOTSSelectManagerComponent)).GetSingletonEntity();
            var selectBuffer = em.GetBuffer<SelectionRequest>(selectmanagerentity);
            selectBuffer.Add(new SelectionRequest
            {
                mode = SelectionMode.Click,
                playerId = playerId,
                targetpos = PhysicConvertHelper.ConvertScreenToWorldPos(currentMousePos, cam),
                v1 = PhysicConvertHelper.ConvertScreenToWorldPos(selectingRect.MinPoint, cam),
                v2 = PhysicConvertHelper.ConvertScreenToWorldPos(selectingRect.MaxPoint, cam),
                v3 = PhysicConvertHelper.ConvertScreenToWorldPos(new float2(selectingRect.MinPoint.x, selectingRect.MaxPoint.y), cam),
                v4 = PhysicConvertHelper.ConvertScreenToWorldPos(new float2(selectingRect.MaxPoint.x, selectingRect.MinPoint.y), cam),
                rayInput = PhysicConvertHelper.GetRayCastInput(currentMousePos, cam, uint.MaxValue)//uint.MaxValue
            });
        }

    }

    public void DragSelect(Vector2 currentMousePos, EntityManager em)
    {
        if (!TryGetPlayerId(out int playerId)) return;
        if(TerrainVisualSurface.Active!=null&&cam!=null) {
            using var query=em.CreateEntityQuery(new EntityQueryDesc {
                All=new[]{ComponentType.ReadOnly<DragSelectableEntity>(),ComponentType.ReadOnly<Selectable>(),ComponentType.ReadOnly<LocalTransform>(),ComponentType.ReadOnly<Selected>()},
                Options=EntityQueryOptions.IgnoreComponentEnabledState
            });
            using var entities=query.ToEntityArray(Allocator.Temp);
            foreach(var entity in entities){
                if(em.GetComponentData<Selectable>(entity).playerID!=playerId)continue;
                Vector3 position=(Vector3)em.GetComponentData<LocalTransform>(entity).Position;
                position.y+=TerrainVisualSurface.Active.Sample(position.x,position.z);
                Vector3 screen=cam.WorldToScreenPoint(position);
                if(screen.z>0&&selectingRect.isContains(new float2(screen.x,screen.y)))em.SetComponentEnabled<Selected>(entity,true);
            }
            return;
        }
      

        if (em != null)
        {
            Entity selectmanagerentity = em.CreateEntityQuery(typeof(DOTSSelectManagerComponent)).GetSingletonEntity();
            var selectBuffer = em.GetBuffer<SelectionRequest>(selectmanagerentity);
            selectBuffer.Add(new SelectionRequest
            {
                mode = SelectionMode.Drag,
                playerId = playerId,
                targetpos = PhysicConvertHelper.ConvertScreenToWorldPos(currentMousePos, cam),
                v1 = PhysicConvertHelper.ConvertScreenToWorldPos(selectingRect.MinPoint, cam),
                v2 = PhysicConvertHelper.ConvertScreenToWorldPos(selectingRect.MaxPoint, cam),
                v3 = PhysicConvertHelper.ConvertScreenToWorldPos(new float2(selectingRect.MinPoint.x, selectingRect.MaxPoint.y), cam),
                v4 = PhysicConvertHelper.ConvertScreenToWorldPos(new float2(selectingRect.MaxPoint.x, selectingRect.MinPoint.y), cam)
            });
        }
    }

    public StartEndRect GetCurrentSelectionRect()
    {
        return selectingRect;
    }


    public static class PhysicConvertHelper
    {
        public static RaycastInput GetRayCastInput(Vector2 screenPos, Camera cam, uint layerMaskFilter)
        {
            UnityEngine.Ray ray = TerrainVisualSurface.LogicalRay(cam.ScreenPointToRay(screenPos));
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

        public static float3 ConvertScreenToWorldPos(Vector2 screenPos, Camera cam)
        {
            if(TerrainVisualSurface.Active!=null && TerrainVisualSurface.Active.Raycast(cam.ScreenPointToRay(screenPos),out var logical,out _)) return logical;
            RaycastInput input = GetRayCastInput(screenPos, cam, PhysicsLayersDefine.Ground);
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
}


public static class SelectHelper
{
    public static bool TryGetLocalPlayerId(out int playerId)
    {
        playerId = -1;
        var manager = GameManager.Instance.GetModule<SelectManager>();
        return manager != null && manager.TryGetPlayerId(out playerId);
    }
    public static Entity GetFirstSelectedEntityByplayerID(int playerId)
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (playerId < 0 || world == null || !world.IsCreated) return Entity.Null;
        var entityManager = world.EntityManager;
        using var selectionQuery = entityManager.CreateEntityQuery(typeof(Selected));
        using var query = selectionQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
        if (query.Length > 0)
        {
            for (int i = 0; i < query.Length; i++)
            {
                if (entityManager.HasComponent<Selectable>(query[i]) && entityManager.GetComponentData<Selectable>(query[i]).playerID == playerId)
                {
                    return query[i];
                }
            }
        }
        return Entity.Null;
    }

    public static List<Entity> GetAllSelectedEntitiesByplayerID(int playerId)
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (playerId < 0 || world == null || !world.IsCreated) return new List<Entity>();
        var entityManager = world.EntityManager;
        using var selectionQuery = entityManager.CreateEntityQuery(typeof(Selected));
        using var query = selectionQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
        List<Entity> selectedEntities = new List<Entity>();
        if (query.Length > 0)
        {
            for (int i = 0; i < query.Length; i++)
            {
                if (entityManager.HasComponent<Selectable>(query[i]) && entityManager.GetComponentData<Selectable>(query[i]).playerID == playerId)
                {
                    selectedEntities.Add(query[i]);
                }
            }
        }
        return selectedEntities;
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
        MinPoint = math.min(StartPoint, EndPoint);
        MaxPoint = math.max(StartPoint, EndPoint);
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
        StartPoint = default(float2);
        EndPoint = default(float2);
    }
}

