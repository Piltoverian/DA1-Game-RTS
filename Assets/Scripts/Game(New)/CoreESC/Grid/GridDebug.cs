using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public enum FlowFieldDebugMode
{
    None,
    Grid,
    Integration,
    Direction
}

public class FlowFieldDebugDrawer : MonoBehaviour
{
    public FlowFieldDebugMode mode = FlowFieldDebugMode.Grid;

    public int drawStep = 4;
    public float arrowScale = 0.4f;

    void OnDrawGizmos()
    {
        if (World.DefaultGameObjectInjectionWorld == null)
            return;

        var em = World.DefaultGameObjectInjectionWorld.EntityManager;

        var query = em.CreateEntityQuery(typeof(GridComponent));

        if (query.IsEmpty)
            return;

        Entity gridEntity = query.GetSingletonEntity();

        GridComponent grid = em.GetComponentData<GridComponent>(gridEntity);
        DynamicBuffer<GridNode> buffer = em.GetBuffer<GridNode>(gridEntity);

        if (buffer.Length == 0)
            return;

        switch (mode)
        {
            case FlowFieldDebugMode.Grid:
                DrawGrid(grid);
                break;

            case FlowFieldDebugMode.Integration:
                DrawIntegration(grid, buffer);
                break;

            case FlowFieldDebugMode.Direction:
                DrawDirection(grid, buffer);
                break;
        }
    }

    void DrawGrid(GridComponent grid)
    {
        Gizmos.color = Color.green;

        for (int x = 0; x <= grid.width; x++)
        {
            Vector3 start = new Vector3(grid.origin.x + x * grid.cellsize, 0, grid.origin.z);
            Vector3 end = new Vector3(grid.origin.x + x * grid.cellsize, 0, grid.origin.z + grid.height * grid.cellsize);

            Gizmos.DrawLine(start, end);
        }

        for (int y = 0; y <= grid.height; y++)
        {
            Vector3 start = new Vector3(grid.origin.x, 0, grid.origin.z + y * grid.cellsize);
            Vector3 end = new Vector3(grid.origin.x + grid.width * grid.cellsize, 0, grid.origin.z + y * grid.cellsize);

            Gizmos.DrawLine(start, end);
        }
    }

    void DrawIntegration(GridComponent grid, DynamicBuffer<GridNode> buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            int x = i % grid.width;
            int y = i / grid.width;

            if (x % drawStep != 0 || y % drawStep != 0)
                continue;

            float worldX = grid.origin.x + (x + 0.5f) * grid.cellsize;
            float worldZ = grid.origin.z + (y + 0.5f) * grid.cellsize;

            int cost = buffer[i].bestcost;

            float t = math.saturate(cost / 500f);

            Gizmos.color = Color.Lerp(Color.red, Color.blue, t);

            Gizmos.DrawCube(
                new Vector3(worldX, 0.1f, worldZ),
                Vector3.one * grid.cellsize * 0.8f
            );
        }
    }

    void DrawDirection(GridComponent grid, DynamicBuffer<GridNode> buffer)
    {
        Gizmos.color = Color.yellow;

        for (int i = 0; i < buffer.Length; i++)
        {
            int x = i % grid.width;
            int y = i / grid.width;

            if (x % drawStep != 0 || y % drawStep != 0)
                continue;

            float worldX = grid.origin.x + (x + 0.5f) * grid.cellsize;
            float worldZ = grid.origin.z + (y + 0.5f) * grid.cellsize;

            float2 dir = buffer[i].direction;

            Vector3 start = new Vector3(worldX, 0.2f, worldZ);
            Vector3 end = start + new Vector3(dir.x, 0, dir.y) * arrowScale;

            Gizmos.DrawLine(start, end);
        }
    }
}