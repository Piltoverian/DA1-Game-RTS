using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

[MaterialProperty("_RevealHeight")]
public struct RevealHeightProperty : IComponentData
{
    public float Value;
}

[BurstCompile]
public partial struct ConstructionSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;

        EntityCommandBuffer ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        ComponentLookup<RevealHeightProperty> revealLookup =
            SystemAPI.GetComponentLookup<RevealHeightProperty>(false);

        ComponentLookup<MaterialMeshInfo> materialMeshLookup =
            SystemAPI.GetComponentLookup<MaterialMeshInfo>(true);

        ComponentLookup<Unity.Transforms.LocalTransform> transformLookup =
            SystemAPI.GetComponentLookup<Unity.Transforms.LocalTransform>(true);

        BufferLookup<LinkedEntityGroup> linkedEntityLookup =
            SystemAPI.GetBufferLookup<LinkedEntityGroup>(true);

        foreach (var (construction, building, entity) in
                 SystemAPI.Query<RefRW<ConstructionData>, RefRO<BuildingData>>()
                     .WithAll<UnderConstructionTag>()
                     .WithEntityAccess())
        {
            float totalWork = math.max(0.001f, building.ValueRO.TotalWorkLoad);

            float progress = math.saturate(construction.ValueRO.currentWorkLoad / totalWork);
            float baseHeight = transformLookup.HasComponent(entity) ? transformLookup[entity].Position.y : 0f;
            float revealValue = baseHeight + math.lerp(-10f, 10f, progress);

            if (progress >= 1f)
            {
                revealValue = baseHeight + 10f;
            }

            SetRevealHeight(entity, revealValue, ref revealLookup, ecb);

            if (linkedEntityLookup.HasBuffer(entity))
            {
                DynamicBuffer<LinkedEntityGroup> linkedEntities = linkedEntityLookup[entity];

                for (int i = 0; i < linkedEntities.Length; i++)
                {
                    Entity linkedEntity = linkedEntities[i].Value;
                    if (linkedEntity == entity) continue;
                    if (!materialMeshLookup.HasComponent(linkedEntity)) continue;

                    SetRevealHeight(linkedEntity, revealValue, ref revealLookup, ecb);
                }
            }

            if (progress >= 1f)
            {
                ecb.RemoveComponent<UnderConstructionTag>(entity);
                ecb.RemoveComponent<ConstructionData>(entity);
            }
        }
    }

    private static void SetRevealHeight(
        Entity entity,
        float value,
        ref ComponentLookup<RevealHeightProperty> revealLookup,
        EntityCommandBuffer ecb)
    {
        if (revealLookup.HasComponent(entity))
        {
            revealLookup[entity] = new RevealHeightProperty { Value = value };
        }
        else
        {
            ecb.AddComponent(entity, new RevealHeightProperty { Value = value });
        }
    }
}