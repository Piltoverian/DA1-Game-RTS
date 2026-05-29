using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

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

        BufferLookup<LinkedEntityGroup> linkedEntityLookup =
            SystemAPI.GetBufferLookup<LinkedEntityGroup>(true);

        foreach (var (construction, entity) in
                 SystemAPI.Query<RefRW<ConstructionData>>()
                     .WithAll<UnderConstructionTag>()
                     .WithEntityAccess())
        {
            construction.ValueRW.Elapsed += dt;

            float totalTime = math.max(0.001f, construction.ValueRO.TotalTime);

            float progress = math.saturate(
                construction.ValueRO.Elapsed / totalTime
            );

            float revealValue = math.lerp(
                construction.ValueRO.StartRevealHeight,
                construction.ValueRO.EndRevealHeight,
                progress
            );

            SetRevealHeight(
                entity,
                revealValue,
                ref revealLookup,
                ecb
            );

            if (linkedEntityLookup.HasBuffer(entity))
            {
                DynamicBuffer<LinkedEntityGroup> linkedEntities =
                    linkedEntityLookup[entity];

                for (int i = 0; i < linkedEntities.Length; i++)
                {
                    Entity linkedEntity = linkedEntities[i].Value;

                    if (linkedEntity == entity)
                        continue;

                    if (!materialMeshLookup.HasComponent(linkedEntity))
                        continue;

                    SetRevealHeight(
                        linkedEntity,
                        revealValue,
                        ref revealLookup,
                        ecb
                    );
                }
            }

            if (progress >= 1f)
            {
                float finalRevealValue = construction.ValueRO.EndRevealHeight;

                SetRevealHeight(
                    entity,
                    finalRevealValue,
                    ref revealLookup,
                    ecb
                );

                if (linkedEntityLookup.HasBuffer(entity))
                {
                    DynamicBuffer<LinkedEntityGroup> linkedEntities =
                        linkedEntityLookup[entity];

                    for (int i = 0; i < linkedEntities.Length; i++)
                    {
                        Entity linkedEntity = linkedEntities[i].Value;

                        if (linkedEntity == entity)
                            continue;

                        if (!materialMeshLookup.HasComponent(linkedEntity))
                            continue;

                        SetRevealHeight(
                            linkedEntity,
                            finalRevealValue,
                            ref revealLookup,
                            ecb
                        );
                    }
                }

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
        RevealHeightProperty reveal = new RevealHeightProperty
        {
            Value = value
        };

        if (revealLookup.HasComponent(entity))
        {
            ecb.SetComponent(entity, reveal);
        }
        else
        {
            ecb.AddComponent(entity, reveal);
        }
    }
}