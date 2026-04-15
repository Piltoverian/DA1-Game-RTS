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

            // 1. Sử dụng EntityQuery để tìm Grid Entity một cách an toàn
            EntityQuery gridQuery = entityManager.CreateEntityQuery(typeof(GridComponent));

            // Thay vì GetSingleton, ta kiểm tra IsEmpty và lấy thực thể đầu tiên tìm thấy
            if (gridQuery.IsEmpty)
            {
                Debug.LogWarning("GridComponent not found in world!");
                return;
            }

            Entity gridEntity = gridQuery.GetSingletonEntity();
            GridComponent gridComponent = entityManager.GetComponentData<GridComponent>(gridEntity);

            // 2. Kiểm tra tính hợp lệ của Grid
            if (gridComponent.width <= 0 || gridComponent.height <= 0) return;

            // Kiểm tra Buffer tồn tại để tránh crash khi gọi API
            if (!entityManager.HasBuffer<GridNodeCost>(gridEntity) ||
                !entityManager.HasBuffer<GridIsland>(gridEntity)) return;

            // 3. Kiểm tra FlowFieldCache Singleton an toàn
            EntityQuery cacheQuery = entityManager.CreateEntityQuery(typeof(FlowFieldCache));
            if (cacheQuery.IsEmpty) return;

            Entity cacheEntity = cacheQuery.GetSingletonEntity();
            if (!entityManager.HasBuffer<FlowFieldCacheEntry>(cacheEntity)) return;

            // 4. Lấy danh sách Unit đang được chọn
            EntityQuery selectedQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<MovementAgentComponent, Selected>()
                .Build(entityManager);

            if (selectedQuery.IsEmpty) return;

            NativeArray<Entity> entityArray = selectedQuery.ToEntityArray(Allocator.Temp);

            // Sử dụng Temp thay vì TempJob vì ta Playback ngay lập tức trên Main Thread
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            for (int i = 0; i < entityArray.Length; i++)
            {
                Entity entity = entityArray[i];
                // Gọi API thiết lập mục tiêu
                MovementAgentAPI.SetTarget(entityManager, entity, mouseWorldPosition, gridComponent, ecb);
            }

            ecb.Playback(entityManager);
            ecb.Dispose();
            entityArray.Dispose();
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
