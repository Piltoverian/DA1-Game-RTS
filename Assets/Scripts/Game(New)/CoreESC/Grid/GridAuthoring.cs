using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using Unity.Physics.Authoring;
using static UnityEditor.PlayerSettings;

public class GridAuthoring : MonoBehaviour
{
    public float MincellSize = 1f;
    public MapType mapType = MapType.Medium;
    public enum MapType
    {
        Small=64,//64x64,plane scale: 12.5.,12.5,12.5
        Medium=128,//128x128,plane scale: 25,25,25
        Large = 256//256x256,plane scale: 50,50,50
    };

    class Baker : Unity.Entities.Baker<GridAuthoring>
    {
        public override void Bake(GridAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Renderable);
            
            Renderer renderer = authoring.GetComponent<Renderer>();
            if(renderer==null)
            {
                Debug.LogError("GridAuthoring requires a Renderer component.");
                return;
            }

            Bounds bounds = renderer.bounds;
            float sizeX = bounds.size.x;
            float sizeY = bounds.size.z;

            float mapTypeSize = (float)authoring.mapType;
            if(sizeX/ authoring.MincellSize < mapTypeSize || sizeY/authoring.MincellSize < mapTypeSize)
            {
                Debug.LogError($"GridAuthoring requires the size of the object to be more than or equal to {mapTypeSize} units in both X and Z dimensions.");
                return;
            }

            GridComponent gridComponent = new GridComponent
            {
                width = (int)mapTypeSize,
                height = (int)mapTypeSize,
                cellsize = sizeX / mapTypeSize,
                origin= bounds.min
            };
            AddComponent(entity, gridComponent);
            AddBuffer<GridNodeCost>(entity);
        }
    }
}

public static class GridHelper
{
    public static int2 WorldToGrid(float3 worldPos, GridComponent grid)
    {
        float xLocal = (worldPos.x - grid.origin.x) / grid.cellsize;
        float yLocal = (worldPos.z - grid.origin.z) / grid.cellsize;
        return new int2((int)math.floor(xLocal), (int)math.floor(yLocal));
    }

    public static float3 GridToWorld(int2 gridPos, GridComponent grid)
    {
            float x = grid.origin.x + gridPos.x * grid.cellsize + grid.cellsize / 2;
            float z = grid.origin.z + gridPos.y * grid.cellsize + grid.cellsize / 2;
            return new float3(x, 0, z);
    }

    public static int GetNodeIndex(int2 gridPos, GridComponent grid)
    {
        return gridPos.y * grid.width + gridPos.x;
    }

    public static int2 GetGridPosFromIndex(int index, GridComponent grid)
    {
        int x = index % grid.width;
        int y = index / grid.width;
        return new int2(x, y);
    }
}