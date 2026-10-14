using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
[UpdateInGroup(typeof(PostBakingSystemGroup))]
[UpdateAfter(typeof(GridSnapperBakingSystem))]
public partial class TerrainHeightBakingSystem : SystemBase
{
    EntityQuery unregistered;
    protected override void OnCreate() => unregistered = TerrainHeightRegistration.CreateQuery(EntityManager, true);
    protected override void OnUpdate()
    {
        TerrainHeightRegistration.Register(EntityManager, unregistered);
        // Incremental baking can change the authored pivot on an already registered root.
        foreach (var (transform, offset, pending) in SystemAPI.Query<RefRO<LocalTransform>, RefRW<TerrainHeightOffset>, EnabledRefRW<PendingHeight>>()
            .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IgnoreComponentEnabledState)) {
            offset.ValueRW.Value = transform.ValueRO.Position.y;
            pending.ValueRW = true;
        }
    }
}

public static class TerrainHeightRegistration
{
    // Missing-component query visits only unregistered roots, never all render children.
    public static void Register(EntityManager em, bool includePrefabs = false)
    {
        using var query = CreateQuery(em, includePrefabs);
        Register(em, query);
    }

    public static EntityQuery CreateQuery(EntityManager em, bool includePrefabs = false)
        => em.CreateEntityQuery(new EntityQueryDesc {
            All = new[] { ComponentType.ReadOnly<LocalTransform>() },
            Any = new[] { ComponentType.ReadOnly<MovementAgentComponent>(),
                ComponentType.ReadOnly<BuildingComponent>(), ComponentType.ReadOnly<ResourceNodeData>() },
            Absent = new[] { ComponentType.ReadOnly<PendingHeight>() },
            Options = includePrefabs ? EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities
                : EntityQueryOptions.Default
        });

    public static void Register(EntityManager em, EntityQuery query)
    {
        if (query.IsEmptyIgnoreFilter) return;
        using var entities = query.ToEntityArray(Allocator.Temp);
        foreach (var entity in entities) {
            if (!em.HasComponent<TerrainHeightOffset>(entity))
                em.AddComponentData(entity, new TerrainHeightOffset { Value = em.GetComponentData<LocalTransform>(entity).Position.y });
            em.AddComponent<PendingHeight>(entity);
            em.SetComponentEnabled<PendingHeight>(entity, true);
        }
    }
}
