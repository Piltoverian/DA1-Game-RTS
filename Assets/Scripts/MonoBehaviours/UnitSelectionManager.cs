using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using System;
using Unity.Transforms;
using Unity.Physics;
using Unity.Physics.Systems;
public class UnitSelectionManager : MonoBehaviour
{
    public static UnitSelectionManager Instance { get; private set; }

    public event EventHandler OnSelectionAreaStart;
    public event EventHandler OnSelectionAreaEnd;

    private Vector2 selectionStartPos;

    private void Awake()
    {
        
            Instance = this;
        
    }
    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            selectionStartPos = Input.mousePosition;
            OnSelectionAreaStart?.Invoke(this, EventArgs.Empty);
        }
        if (Input.GetMouseButtonUp(0))
        {
            Vector2 selectionEndPos = Input.mousePosition;

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            EntityQuery entityQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Selected>().Build(entityManager);

            NativeArray<Entity> entityArray = entityQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entityArray.Length; i++)
            {
                entityManager.SetComponentEnabled<Selected>(entityArray[i], false);
            }

            

            Rect selectionArea = GetSelectionArea();

            float selectionAreaSize = selectionArea.width + selectionArea.height;
            float multipleselectinSizeMin = 40f;
            bool isMultipleSelection = selectionAreaSize > multipleselectinSizeMin;

            if (isMultipleSelection)
            {
                entityQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<LocalTransform, Unit>().WithPresent<Selected>().Build(entityManager);

                entityArray = entityQuery.ToEntityArray(Allocator.Temp);
                NativeArray<LocalTransform> localTransformArray = entityQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                for (int i = 0; i < localTransformArray.Length; i++)
                {
                    LocalTransform unitlocalTransform = localTransformArray[i];
                    Vector2 unitScreenPosition = Camera.main.WorldToScreenPoint(unitlocalTransform.Position);
                    if (selectionArea.Contains(unitScreenPosition))
                    {
                        entityManager.SetComponentEnabled<Selected>(entityArray[i], true);
                    }
                }
            }
            else
            {
                EntityQuery physicsQuery = entityManager.CreateEntityQuery(typeof(PhysicsWorldSingleton));
                if (physicsQuery.IsEmpty) return;

                PhysicsWorldSingleton physicsWorld = physicsQuery.GetSingleton<PhysicsWorldSingleton>();
                CollisionWorld collisionWorld = physicsWorld.CollisionWorld;
                UnityEngine.Ray cameraRay = Camera.main.ScreenPointToRay(Input.mousePosition);
                int unitsLayer = 6;
                RaycastInput raycastInput = new RaycastInput
                {
                    Start = cameraRay.GetPoint(0f),
                    End = cameraRay.GetPoint(9999f),
                    Filter = new CollisionFilter
                    {
                        BelongsTo = ~0u,
                        CollidesWith = 1u << GameAssets.UNITS_LAYER, 
                        GroupIndex = 0
                    }
                };

                if (collisionWorld.CastRay(raycastInput, out Unity.Physics.RaycastHit raycastHit))
                {
                    if (entityManager.HasComponent<Unit>(raycastHit.Entity) && entityManager.HasComponent<Selected>(raycastHit.Entity))
                    {
                        entityManager.SetComponentEnabled<Selected>(raycastHit.Entity, true);
                    }
                }
            }

                OnSelectionAreaEnd?.Invoke(this, EventArgs.Empty);
        }
        if (Input.GetMouseButtonDown(1))
        {
            Vector3 mouseWorldPosition = MouseWorldPosition.Instance.GetPosition();

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            EntityQuery entityQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<UnitMover,Selected>()
                .Build(entityManager);

            NativeArray<Entity> entityArray = entityQuery.ToEntityArray(Allocator.Temp);
            NativeArray<UnitMover> unitMoverArray = entityQuery.ToComponentDataArray<UnitMover>(Allocator.Temp);

            for ( int i = 0; i < entityArray.Length; i++)
            {
                Entity entity = entityArray[i];
                UnitMover unitMover = unitMoverArray[i];
                // Update the target position of the UnitMover component
                unitMover.targetPosition = mouseWorldPosition;
                unitMoverArray[i] = unitMover; // Write back the modified component data
            }
            entityQuery.CopyFromComponentDataArray(unitMoverArray);

        }
    }
    public Rect GetSelectionArea()
    {
        Vector2 selectionEndPos = Input.mousePosition;

        Vector2 lowerLeftCorner = new Vector2(
            Mathf.Min(selectionStartPos.x, selectionEndPos.x),
            Mathf.Min(selectionStartPos.y, selectionEndPos.y)
        );
        Vector2 upperRightCorner = new Vector2(
            Mathf.Max(selectionStartPos.x, selectionEndPos.x),
            Mathf.Max(selectionStartPos.y, selectionEndPos.y)
        );
        return new Rect(lowerLeftCorner, upperRightCorner - lowerLeftCorner);
    }
}
