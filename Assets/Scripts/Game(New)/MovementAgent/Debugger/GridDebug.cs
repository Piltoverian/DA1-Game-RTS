using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;

public enum FlowFieldDebugMode
{
    None,
    Grid,
    Islands,      // Chế độ xem các hòn đảo
    Integration,  // Chi phí tích lũy
    Direction     // Hướng di chuyển
}

public class FlowFieldDebugDrawer : MonoBehaviour
{
    public FlowFieldDebugMode mode = FlowFieldDebugMode.Grid;

    [Header("Settings")]
    public int drawStep = 1;      // Vẽ chi tiết hơn (mặc định = 1)
    public float arrowScale = 0.5f;
    public bool showLabel = false; // Hiển thị số liệu (Cost/IslandID)

    [Header("Multi-Field Selection")]
    public int targetFieldIndex = 0; // Chọn Field thứ mấy để hiển thị

    void OnDrawGizmos()
    {
        if (!Application.isPlaying || World.DefaultGameObjectInjectionWorld == null)
            return;

        var em = World.DefaultGameObjectInjectionWorld.EntityManager;

        // 1. Lấy Grid Entity
        var gridQuery = em.CreateEntityQuery(typeof(GridComponent));
        if (gridQuery.IsEmpty) return;
        Entity gridEntity = gridQuery.GetSingletonEntity();
        GridComponent grid = em.GetComponentData<GridComponent>(gridEntity);

        // 2. Xử lý theo Mode
        switch (mode)
        {
            case FlowFieldDebugMode.Grid:
                DrawGrid(grid);
                break;

            case FlowFieldDebugMode.Islands:
                if (em.HasBuffer<GridIsland>(gridEntity))
                {
                    DrawIslands(grid, em.GetBuffer<GridIsland>(gridEntity));
                }
                break;

            case FlowFieldDebugMode.Integration:
            case FlowFieldDebugMode.Direction:
                DrawFlowField(em, grid);
                break;
        }
    }

    void DrawGrid(GridComponent grid)
    {
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        for (int x = 0; x <= grid.width; x++)
        {
            Vector3 start = grid.origin + new float3(x * grid.cellsize, 0, 0);
            Vector3 end = grid.origin + new float3(x * grid.cellsize, 0, grid.height * grid.cellsize);
            Gizmos.DrawLine(start, end);
        }
        for (int y = 0; y <= grid.height; y++)
        {
            Vector3 start = grid.origin + new float3(0, 0, y * grid.cellsize);
            Vector3 end = grid.origin + new float3(grid.width * grid.cellsize, 0, y * grid.cellsize);
            Gizmos.DrawLine(start, end);
        }
    }

    void DrawIslands(GridComponent grid, DynamicBuffer<GridIsland> islands)
    {
        for (int i = 0; i < islands.Length; i++)
        {
            int id = islands[i].islandID;
            if (id <= 0) continue;

            int2 pos = GridHelper.GetGridPosFromIndex(i, grid);
            if (pos.x % drawStep != 0 || pos.y % drawStep != 0) continue;

            // Tạo màu ngẫu nhiên nhưng cố định theo ID
            Unity.Mathematics.Random r = new Unity.Mathematics.Random((uint)id * 7823); 
            Gizmos.color = new Color(r.NextFloat(0.2f, 1f), r.NextFloat(0.2f, 1f), r.NextFloat(0.2f, 1f), 0.5f);

            float3 worldPos = GridHelper.GridToWorld(pos, grid);
            Gizmos.DrawCube(worldPos + new float3(0, 0.05f, 0), new float3(grid.cellsize * 0.9f, 0.1f, grid.cellsize * 0.9f));
        }
    }

    void DrawFlowField(EntityManager em, GridComponent grid)
    {
        // Tìm tất cả các thực thể có FlowField
        using var fieldQuery = em.CreateEntityQuery(typeof(FlowField), typeof(FieldNode));
        var entities = fieldQuery.ToEntityArray(Allocator.Temp);
        
        if (entities.Length == 0) return;

        // Giới hạn index hợp lệ
        int index = math.clamp(targetFieldIndex, 0, entities.Length - 1);
        Entity targetEntity = entities[index];
        var buffer = em.GetBuffer<FieldNode>(targetEntity);

        for (int i = 0; i < buffer.Length; i++)
        {
            int2 pos = GridHelper.GetGridPosFromIndex(i, grid);
            if (pos.x % drawStep != 0 || pos.y % drawStep != 0) continue;

            float3 worldPos = GridHelper.GridToWorld(pos, grid);
            FieldNode node = buffer[i];

            if (mode == FlowFieldDebugMode.Integration)
            {
                if (node.bestcost == int.MaxValue) continue;
                float t = math.saturate(node.bestcost / 1000f);
                Gizmos.color = Color.Lerp(Color.blue, Color.red, t);
                Gizmos.DrawCube(worldPos + new float3(0, 0.1f, 0), new float3(grid.cellsize * 0.8f, 0.05f, grid.cellsize * 0.8f));
            }
            else if (mode == FlowFieldDebugMode.Direction)
            {
                if (math.lengthsq(node.direction) < 0.001f) continue;
                Gizmos.color = Color.yellow;
                Vector3 end = (Vector3)worldPos + new Vector3(node.direction.x, 0, node.direction.y) * arrowScale;
                Gizmos.DrawLine(worldPos + new float3(0, 0.2f, 0), (float3)end + new float3(0, 0.2f, 0));
            }
        }
        entities.Dispose();
    }
}