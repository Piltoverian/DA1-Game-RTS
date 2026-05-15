using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public partial struct ConstructionSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;

        var ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (con, revealHeight, entity) in
                 SystemAPI.Query<
                         RefRW<ConstructionData>,
                         RefRW<RevealHeightProperty>>()
                     .WithAll<UnderConstructionTag>()
                     .WithEntityAccess())
        {
            con.ValueRW.Elapsed += dt;

            float progress = math.saturate(
                con.ValueRO.Elapsed / con.ValueRO.TotalTime
            );

            revealHeight.ValueRW.Value = math.lerp(
                con.ValueRO.StartRevealHeight,
                con.ValueRO.EndRevealHeight,
                progress
            );

            if (progress >= 1f)
            {
                ecb.RemoveComponent<UnderConstructionTag>(entity);
                ecb.RemoveComponent<ConstructionData>(entity);
            }
        }
    }
}
