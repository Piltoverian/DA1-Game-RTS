using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(MovementAgentTargetSystem))]
[UpdateAfter(typeof(MovementAgentAvoidanceSystem))]
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
            float3 targetVelocity = move.velocity;
            float3 lastAvoidDir = avoidance.lastAvoidDir;

            // --- 1. WEIGHTED MULTI-LAYER BLENDING ---
            // Thay thế việc cộng vector đơn giản bằng hệ thống trọng số cân bằng
            float3 goalDir = math.lengthsq(targetVelocity) > 0.001f ? math.normalize(targetVelocity) : float3.zero;
            float3 avoidDir = math.lengthsq(lastAvoidDir) > 0.001f ? math.normalize(lastAvoidDir) : float3.zero;

            // Proximity urgency: càng gần → avoidWeight càng cao, dựa trên khoảng cách so với radius
            float proximityUrgency = 0f;
            if (avoidance.closestDistance < float.MaxValue && avoidance.closestDistance > 0.001f)
            {
                float dangerRatio = avoidance.radius * 2.0f / avoidance.closestDistance;
                proximityUrgency = math.clamp(dangerRatio, 0f, 1f);
            }
            float countWeight = math.clamp(avoidance.neighborCount * 0.15f, 0f, 0.6f);
            float avoidWeight = math.clamp(math.max(countWeight, proximityUrgency), 0f, 0.85f);
            if (avoidance.neighborCount == 0) avoidWeight = 0;

            float3 blendDir = math.lerp(goalDir, avoidDir, avoidWeight);
            float3 finalDir = math.lengthsq(blendDir) > 0.001f ? math.normalize(blendDir) : float3.zero;

            // --- 2. ANTI-DEADLOCK INCREMENT & EARLY SETTLE ---
            if (move.hastarget)
            {
                steering.stuckTime += DeltaTime;

                float distToGlobal = math.distance(pos, move.currentworldtarget);
                float stuckThreshold = distToGlobal < steering.formationRange ? 1.0f : 2.0f;

                if (steering.stuckTime > stuckThreshold)
                {
                    move.hastarget = false;
                    move.velocity = float3.zero;
                    steering.isSettled = true;
                    steering.stuckTime = 0;
                    steering.minDistanceToTarget = float.MaxValue;
                    return;
                }
            }

            float3 desiredVelocity = finalDir * move.speed;

            // --- 3. SEPARATION FORCE (PUSHING) ---
            if (math.lengthsq(avoidance.separationForce) > 0.01f)
            {
                // Khôi phục hằng số cũ: 2.0f khi đang đi, 1.5f khi đứng yên
                float pushForce = move.hastarget ? 2.0f : 1.5f;
                desiredVelocity += avoidance.separationForce * move.speed * pushForce;
            }

            // --- 3. ANTI-DEADLOCK (NUDGE) ---
            if (move.hastarget && steering.stuckTime > 0.5f)
            {
                uint seed = (uint)(entity.Index + 1) * 1523u + (uint)(steering.stuckTime * 10000f + 1f);
                var random = new Unity.Mathematics.Random(math.max(seed, 1u));
                float3 nudge = random.NextFloat3Direction();
                nudge.y = 0;
                float distToGlobal = math.distance(pos, move.currentworldtarget);
                float nudgeMult = distToGlobal < steering.formationRange ? 0.2f : 0.5f;
                desiredVelocity += nudge * move.speed * nudgeMult;
            }

            // --- 4. UPDATE VELOCITY & POSITION ---
            float distToGlobalActual = math.distance(pos, move.currentworldtarget);
            float baseLerp = distToGlobalActual < steering.formationRange ? 5.0f : 10.0f;
            // Tăng tốc xoay hướng khi gần obstacle — phản ứng nhanh hơn thay vì chờ lerp chậm
            float urgencyBoost = proximityUrgency > 0.5f ? 2.0f : 1.0f;
            float lerpFactor = baseLerp * urgencyBoost;
            move.velocity = math.lerp(move.velocity, desiredVelocity, DeltaTime * lerpFactor);

            if (math.lengthsq(move.velocity) > 0.001f)
            {
                transform.Position += move.velocity * DeltaTime;
                steering.isSettled = false;
            }
            else
            {
                steering.isSettled = true;
            }

            // --- HARD COLLISION RESOLUTION (Direct Position Correction) ---
            // Khi overlap nghiêm trọng, bypass velocity lerp và đẩy trực tiếp position
            // Đây là lớp "safety net" — giải quyết overlap trong 1-2 frames thay vì 5-10
            if (avoidance.closestDistance < avoidance.radius * 1.8f &&
                math.lengthsq(avoidance.separationForce) > 0.01f)
            {
                float overlapDepth = 1.0f - avoidance.closestDistance / (avoidance.radius * 1.8f);
                float3 pushDir = math.normalizesafe(avoidance.separationForce);
                float3 directCorrection = pushDir * overlapDepth * avoidance.radius * DeltaTime * 5.0f;
                directCorrection.y = 0;
                transform.Position += directCorrection;
            }

            // --- 5. ROTATION LOGIC ---
            if (math.lengthsq(move.velocity) > 0.01f)
            {
                float3 moveDir = math.normalize(move.velocity);
                quaternion targetRot = quaternion.LookRotationSafe(moveDir, math.up());
                transform.Rotation = math.slerp(transform.Rotation, targetRot, DeltaTime * 8.0f);
            }
        }
    }
}
