using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
[UpdateInGroup(typeof(PostBakingSystemGroup))]
public partial class GridSnapperBakingSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var em = EntityManager;
        using var grids = em.CreateEntityQuery(typeof(GridComponent));
        if (grids.IsEmpty) return;
        var grid = grids.GetSingleton<GridComponent>();
        var store = World.GetExistingSystemManaged<BakingSystem>().BlobAssetStore;
        using var query = em.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<GridSnapperSource>(), ComponentType.ReadWrite<GridFootprint>(),
                ComponentType.ReadWrite<LocalTransform>(), ComponentType.ReadWrite<BlockageData>() },
            Options = EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities
        });
        using var entities = query.ToEntityArray(Allocator.Temp);
        foreach (var entity in entities)
        {
            var source = em.GetComponentData<GridSnapperSource>(entity);
            if (math.any(source.Size <= 0) || !math.all(math.isfinite(source.Size)))
                throw new InvalidOperationException("Grid snapper source collider must have positive finite dimensions.");
            var footprint = em.GetComponentData<GridFootprint>(entity);
            float side = footprint.Cells * grid.cellsize;
            float3 resize = GridSnapperMath.Scale(source.Size, footprint.Cells, grid.cellsize);
            float3 center = source.Center * resize;
            var transform = em.GetComponentData<LocalTransform>(entity);
            transform.Scale = 1;
            transform.Rotation = quaternion.identity;
            if (!em.HasComponent<Prefab>(entity)) transform.Position = GridSnapperMath.Snap(transform.Position, grid, footprint.Cells);
            em.SetComponentData(entity, transform);
            // Art and child transforms follow the same resizing and XZ recentering as the collider.
            var matrix = new PostTransformMatrix { Value = float4x4.TRS(new float3(-center.x, 0, -center.z),
                quaternion.identity, source.RootScale * resize) };
            if (em.HasComponent<PostTransformMatrix>(entity)) em.SetComponentData(entity, matrix);
            else em.AddComponentData(entity, matrix);
            var rect = new StartEndRect(new float2(-side * .5f));
            rect.ExpandTo(new float2(side * .5f));
            var blockage = em.GetComponentData<BlockageData>(entity);
            blockage.LocalRect = rect;
            em.SetComponentData(entity, blockage);
            if (em.HasComponent<BlockageCleanupData>(entity))
                em.SetComponentData(entity, new BlockageCleanupData { Position = transform.Position, LocalRect = rect });
            if (em.HasComponent<PhysicsCollider>(entity))
            {
                var physics = em.GetComponentData<PhysicsCollider>(entity);
                var filter = physics.Value.Value.GetCollisionFilter();
                // Root box input is intentionally required; the exact square is shared by physics and navigation.
                var collider = Unity.Physics.BoxCollider.Create(new BoxGeometry
                {
                    Center = new float3(0, center.y, 0), Orientation = quaternion.identity,
                    Size = new float3(side, source.Size.y * resize.y, side), BevelRadius = 0
                }, filter);
                store.TryAdd(ref collider);
                physics.Value = collider;
                em.SetComponentData(entity, physics);
            }
            footprint.WorldSize = side;
            em.SetComponentData(entity, footprint);
        }
    }
}
