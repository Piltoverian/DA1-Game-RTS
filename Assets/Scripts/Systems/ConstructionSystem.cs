using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public partial class ConstructionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;

        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (con, revealHeight, entity) in
                 SystemAPI.Query<RefRW<ConstructionData>, RefRW<RevealHeightProperty>>()
                     .WithAll<UnderConstructionTag>()
                     .WithEntityAccess())
        {
            con.ValueRW.Elapsed += dt;

            float progress = math.saturate(con.ValueRO.Elapsed / con.ValueRO.TotalTime);

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

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}