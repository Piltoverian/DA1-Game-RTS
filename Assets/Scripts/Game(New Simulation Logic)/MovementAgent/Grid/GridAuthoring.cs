using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class GridAuthoring : MonoBehaviour
{
    public float MincellSize = 1f;
    public MapType mapType = MapType.Medium;


    [Header("Full terrain generation (opt-in)")]
    public bool generateTerrain;
    public TerrainGenerationSettings terrainSettings = new TerrainGenerationSettings();

    public GridComponent GetGridDefinition()
    {
        var renderer = GetComponent<Renderer>();
        if (!renderer) throw new System.InvalidOperationException("GridAuthoring requires a Renderer");
        Bounds bounds = renderer.bounds;
        int size = (int)mapType;
        if (MincellSize <= 0 || bounds.size.x / MincellSize < size || bounds.size.z / MincellSize < size)
            throw new System.InvalidOperationException("Plane is too small for requested grid");
        if (math.abs(bounds.size.x - bounds.size.z) > .001f * math.max(bounds.size.x, bounds.size.z))
            throw new System.InvalidOperationException("GridAuthoring needs a square, axis-aligned Plane");
        return new GridComponent { width = size, height = size, cellsize = bounds.size.x / size,
            origin = bounds.min, generation = 0, islandGeneration = uint.MaxValue };
    }

    public enum MapType
    {
        Small=64,//64x64,plane scale: 12.5.,12.5,12.5
        Medium=128,//128x128,plane scale: 25,25,25
        Large = 256,//256x256,plane scale: 50,50,50

        Extreme = 512//512x512,plane scale: 100,100,100
    };

    class Baker : Unity.Entities.Baker<GridAuthoring>
    {
        public override void Bake(GridAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Renderable);
            
            GridComponent gridComponent = authoring.GetGridDefinition();
            AddComponent(entity, gridComponent);
            AddBuffer<GridNodeCost>(entity);
            AddBuffer<GridIsland>(entity);
            AddBuffer<CostChangeRequest>(entity);
            var terrainBuffer = AddBuffer<GridTerrain>(entity);


            if (authoring.generateTerrain)
            {
                // TerrainMapRenderer renders the baked cells; retain the plane collider.
                AddComponent<Unity.Rendering.DisableRendering>(entity);
                var terrain = TerrainGeneration.Generate(gridComponent, authoring.terrainSettings);
                foreach (var cell in terrain.cells) terrainBuffer.Add(cell);
                var spawns = AddBuffer<GridSpawnCell>(entity);
                for (int player = 0; player < terrain.spawns.Length; player++)
                    spawns.Add(new GridSpawnCell { playerId = player, cell = terrain.spawns[player] });
            }
        }
    }
}
