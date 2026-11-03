using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

// Recreate material presentation from replicated construction progress; never advance gameplay here.
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateBefore(typeof(EntitiesGraphicsSystem))]
public partial class NetworkConstructionVisualSystem : SystemBase
{
    protected override void OnUpdate()
    {
        using var commands = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (building, construction, transform, entity) in
            SystemAPI.Query<RefRO<BuildingComponent>, RefRO<BuildingConstruction>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            float work = building.ValueRO.WorkLoad;
            if (!math.isfinite(work) || work <= 0) continue;
            float value = transform.ValueRO.Position.y + math.lerp(-10, 10, math.saturate(construction.ValueRO.CompletedWork / work));
            Apply(entity, value, commands);
            if (!EntityManager.HasBuffer<LinkedEntityGroup>(entity)) continue;
            foreach (var child in EntityManager.GetBuffer<LinkedEntityGroup>(entity, true))
                if (child.Value != entity && EntityManager.HasComponent<MaterialMeshInfo>(child.Value)) Apply(child.Value, value, commands);
        }
        commands.Playback(EntityManager);
    }
    void Apply(Entity entity, float value, EntityCommandBuffer commands)
    {
        var data = new RevealHeightProperty { Value = value };
        if (EntityManager.HasComponent<RevealHeightProperty>(entity)) commands.SetComponent(entity, data);
        else commands.AddComponent(entity, data);
    }
}
