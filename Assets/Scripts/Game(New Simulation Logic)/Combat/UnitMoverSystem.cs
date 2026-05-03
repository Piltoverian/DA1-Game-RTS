using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Physics;
public partial struct UnitMoverSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        new UnitMoverJob
        {
            deltaTime = SystemAPI.Time.DeltaTime
        }.ScheduleParallel();
        //float deltaTime = SystemAPI.Time.DeltaTime;

        //foreach ((RefRW<LocalTransform> transform,
        //     RefRO<UnitMover> unitMover,
        //     RefRW<PhysicsVelocity> physicsVelocity)

        //in SystemAPI.Query<
        //    RefRW<LocalTransform>,
        //    RefRO<UnitMover>,
        //    RefRW<PhysicsVelocity>>())
        //{

        //    float3 moveDir = unitMover.ValueRO.targetPosition - transform.ValueRO.Position;
        //    float3 direction = math.lengthsq(moveDir) > 0 ? math.normalize(moveDir) : float3.zero;

        //    if (!math.all(direction == float3.zero))
        //    {
        //        quaternion targetRot = quaternion.LookRotationSafe(direction, math.up());
        //        transform.ValueRW.Rotation = math.slerp(
        //            transform.ValueRO.Rotation,
        //            targetRot,
        //            unitMover.ValueRO.rotationSpeed * deltaTime
        //        );
        //    }

        //    physicsVelocity.ValueRW.Linear = direction * unitMover.ValueRO.moveSpeed;
        //    physicsVelocity.ValueRW.Angular = float3.zero;
        //}
    }
}
[BurstCompile]
public partial struct UnitMoverJob : IJobEntity
{
    public float deltaTime;

    public void Execute(ref LocalTransform transform, ref PhysicsVelocity velocity, in UnitMover mover)
    {
        float3 vectorToTarget = mover.targetPosition - transform.Position;
        if (math.lengthsq(vectorToTarget) > 0.2f)
        {
            float3 direction = math.normalize(vectorToTarget);
            quaternion targetRotation = quaternion.LookRotationSafe(direction, math.up());
            transform.Rotation = math.slerp(transform.Rotation, targetRotation, mover.rotationSpeed * deltaTime);
            velocity.Linear = direction * mover.moveSpeed;
        }
        else {             velocity.Linear = float3.zero;
        }
         velocity.Angular = float3.zero;
    }
}