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
                steering.stuckTime += DeltaTime;

                float distToGlobal = math.distance(pos, move.currentworldtarget);

                // Stuck threshold phụ thuộc vào MÔI TRƯỜNG:
                // - Bị chặn bởi neighbor gần → settle RẤT NHANH (0.25s)
                //   → "first come, settle first" → tạo tường → unit sau dồn ra rìa
                // - Gần target nhưng không bị chặn → settle trung bình (1.0s)
                // - Xa target → settle chậm (2.0s) → cho thời gian tìm đường
                float stuckThreshold;
                bool blockedByNeighbor = avoidance.closestDistance < avoidance.radius * 2.5f
                                         && avoidance.neighborCount > 0;

                if (blockedByNeighbor)
                    stuckThreshold = 0.25f; // ~12 frames → settle nhanh khi bị chặn
                else if (distToGlobal < steering.formationRange)
                    stuckThreshold = 1.0f;
                else
                    stuckThreshold = 2.0f;

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
            // Push ra từ TẤT CẢ overlapping neighbors (cumulative từ ORCASystem)
            // Giải overlap từ frame trước TRƯỚC KHI apply velocity mới
            // Position-based → đảm bảo 100% không overlap, KHÔNG phụ thuộc ORCA/stuck
            if (math.lengthsq(avoidance.separationForce) > 0.001f)
            {
                float3 pushDir = math.normalizesafe(avoidance.separationForce);
                float pushMag = math.length(avoidance.separationForce);
                // pushMag = sum of (1-dist/combined) cho mỗi neighbor overlap
                // Scale với radius để correction tương ứng kích thước agent
                float3 correction = pushDir * pushMag * avoidance.radius;
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
                float3 moveDir = math.normalize(move.velocity);
                quaternion targetRot = quaternion.LookRotationSafe(moveDir, math.up());
                transform.Rotation = math.slerp(transform.Rotation, targetRot, DeltaTime * 8.0f);
            }
        }
    }
}
