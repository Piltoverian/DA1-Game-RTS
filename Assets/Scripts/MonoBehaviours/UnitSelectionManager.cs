using UnityEngine;
using Unity.Entities;
using Unity.Collections;

public class UnitSelectionManager : MonoBehaviour
{
    public static UnitSelectionManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
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
                .WithPresent<MoveOverride>()  
                .Build(entityManager);

            if (selectedQuery.IsEmpty) return;

            NativeArray<Entity> entityArray = selectedQuery.ToEntityArray(Allocator.Temp);

            // Sử dụng Temp thay vì TempJob vì ta Playback ngay lập tức trên Main Thread
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            for (int i = 0; i < entityArray.Length; i++)
            {
                Entity entity = entityArray[i];
                MoveOverride moveData = entityManager.GetComponentData<MoveOverride>(entity);

                moveData.targetPosition = mouseWorldPosition;
                moveData.targetApplied = false;

                ecb.SetComponent(entity, moveData); // Ghi đè lại struct đã được cập nhật
                ecb.SetComponentEnabled<MoveOverride>(entity, true);
            }

            ecb.Playback(entityManager);
            ecb.Dispose();
            entityArray.Dispose();
        }
    }
}

