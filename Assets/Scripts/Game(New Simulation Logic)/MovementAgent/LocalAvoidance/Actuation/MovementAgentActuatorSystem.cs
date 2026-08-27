using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// ActuatorSystem (ORCA pipeline): Apply velocity từ ORCASystem vào position.
/// 
/// Pipeline: TargetSystem → ORCASystem → ActuatorSystem
/// 
/// Vai trò:
///   1. Anti-deadlock: stuck detection + force settle
///   2. Apply velocity → position (trực tiếp, không smoothing thêm)
///   3. Safety net: hard collision nếu ORCA fail (hiếm khi xảy ra)
///   4. Rotation
/// </summary>
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(MovementAgentORCASystem))]
public partial struct MovementAgentActuatorSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var deltaTime = SystemAPI.Time.DeltaTime;

        var job = new MovementAgentActuatorJob
        {
            DeltaTime = deltaTime
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public partial struct MovementAgentActuatorJob : IJobEntity
    {
        public float DeltaTime;

        public void Execute(Entity entity, ref LocalTransform transform,
            ref MovementAgentComponent move,
            ref MovementSteeringComponent steering,
            [ReadOnly] in MovementAgentAvoidanceComponent avoidance)
        {
            float3 pos = transform.Position;

            // --- 1. ANTI-DEADLOCK: Stuck Detection ---
            if (move.hastarget)
            {
                // Tính khoảng cách THỰC TẾ di chuyển được so với frame trước
                float actualDistMoved = math.distance(pos, steering.lastPosition);
                float expectedDist = move.speed * DeltaTime;
                
                // Cập nhật lastPosition cho frame tiếp theo
                steering.lastPosition = pos;

                // Nếu đi được < 10% so với lý thuyết (bị chặn vật lý)
                if (actualDistMoved < expectedDist * 0.1f)
                    steering.stuckTime += DeltaTime;
                else
                    steering.stuckTime += DeltaTime * 0.2f; // Vẫn đang lách -> tăng chậm

                float distToGlobal = math.distance(pos, move.currentworldtarget);

                float stuckThreshold;
                bool blockedByNeighbor = avoidance.closestDistance < avoidance.radius * 2.5f
                                         && avoidance.neighborCount > 0;

                if (blockedByNeighbor)
                    stuckThreshold = 1.0f;
                else if (distToGlobal < steering.formationRange)
                    stuckThreshold = 1.5f;
                else
                    stuckThreshold = 2.5f;

                if (steering.stuckTime > stuckThreshold)
                {
                    // Settle bình thường — khi settled, prefVel = 0
                    // → separation trở thành lực duy nhất → overlap tự giải
                    move.hastarget = false;
                    move.velocity = float3.zero;
                    move.preferredVelocity = float3.zero;
                    steering.isSettled = true;
                    steering.stuckTime = 0;
                    steering.minDistanceToTarget = float.MaxValue;
                    return;
                }
            }

            // --- 2. APPLY VELOCITY (từ ORCA) ---
            // --- 2. SAFETY NET: Position Correction (chạy TRƯỚC velocity) ---
            if (math.lengthsq(avoidance.separationForce) > 0.001f)
            {
                float3 pushDir = math.normalizesafe(avoidance.separationForce);
                float pushMag = math.length(avoidance.separationForce);
                
                float3 correction = pushDir * pushMag * avoidance.radius;
                
                // GIỚI HẠN LỰC ĐẨY: Không cho phép Unit bị teleport đi quá xa trong 1 frame
                // Tối đa trượt đi 1 khoảng bằng 1.5 lần bán kính của nó mỗi frame
                float maxCorrection = avoidance.radius * 1.5f;
                if (math.length(correction) > maxCorrection)
                {
                    correction = math.normalizesafe(correction) * maxCorrection;
                }
                
                correction.y = 0;
                transform.Position += correction;
            }

            // --- 3. APPLY VELOCITY (từ ORCA) ---
            if (math.lengthsq(move.velocity) > 0.001f)
            {
                transform.Position += move.velocity * DeltaTime;
                steering.isSettled = false;
            }
            else
            {
                steering.isSettled = true;
            }

            // --- 4. ROTATION ---
            if (math.lengthsq(move.velocity) > 0.01f)
            {
                // Xoay theo hướng di chuyển
                float3 moveDir = math.normalizesafe(move.velocity);
                quaternion targetRot = quaternion.LookRotationSafe(moveDir, math.up());
                transform.Rotation = math.slerp(transform.Rotation, targetRot, DeltaTime * steering.rotationSpeed);
            }
            else if (steering.isSettled)
            {
                // Khi đã dừng lại, xoay mặt về đích (lookAtPoint hoặc currentworldtarget)
                float3 lookTarget = math.lengthsq(move.lookAtPoint) > 0.01f ? move.lookAtPoint : move.currentworldtarget;
                float3 lookDir = lookTarget - transform.Position;
                lookDir.y = 0;
                
                if (math.lengthsq(lookDir) > 0.01f)
                {
                    quaternion targetRot = quaternion.LookRotationSafe(math.normalizesafe(lookDir), math.up());
                    transform.Rotation = math.slerp(transform.Rotation, targetRot, DeltaTime * steering.rotationSpeed * 0.5f);
                }
            }
        }
    }
}
