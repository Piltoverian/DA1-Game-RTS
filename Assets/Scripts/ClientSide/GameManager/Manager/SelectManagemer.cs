using gameManagerModule;
using NUnit.Framework;
using System.Collections.Generic;
using System.Drawing;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Mathematics;
using Unity.Physics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
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
            SingleSelecting(MousePos, em);
            return;
        }
        else if (GameManager.Instance.GetModule<FixedUpdateInputTracker>().IsHolding(Mouse.current.leftButton)&&holdbuffer>0.1)
        {
            if (!selectingRect.isNotNull)
            {
                selectingRect = new StartEndRect(MousePos);
            }
            else
            {
                selectingRect.ExpandTo(MousePos);
                DragSelect(MousePos,em);
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
                v4 = PhysicConvertHelper.ConvertScreenToWorldPos(new float2(selectingRect.MaxPoint.x, selectingRect.MinPoint.y), cam)
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
    public static RaycastInput GetRayCastInput(Vector2 screenPos,Camera cam)
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
                CollidesWith = PhysicsLayersDefine.Ground
            }
        };

        return raycastInput;
    }

    public static float3 ConvertScreenToWorldPos(Vector2 screenPos,Camera cam)
    {
        RaycastInput input = GetRayCastInput(screenPos,cam);
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