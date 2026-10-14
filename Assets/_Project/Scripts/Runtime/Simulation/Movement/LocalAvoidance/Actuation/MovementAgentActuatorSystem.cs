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
///   1. Stuck detection + settle only at a crowded destination
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
        bool hasGrid = SystemAPI.TryGetSingleton<GridComponent>(out var grid);

        var job = new MovementAgentActuatorJob
        {
            DeltaTime = deltaTime,
            Grid = grid,
            GridEntity = hasGrid ? SystemAPI.GetSingletonEntity<GridComponent>() : Entity.Null,
            GridCostsLookup = SystemAPI.GetBufferLookup<GridNodeCost>(true),
            PendingHeightLookup = SystemAPI.GetComponentLookup<PendingHeight>(false)
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public partial struct MovementAgentActuatorJob : IJobEntity
    {
        public float DeltaTime;
        public GridComponent Grid;
        public Entity GridEntity;
        [ReadOnly] public BufferLookup<GridNodeCost> GridCostsLookup;
        [Unity.Collections.NativeDisableParallelForRestriction]
        public ComponentLookup<PendingHeight> PendingHeightLookup;

        public void Execute(Entity entity, ref LocalTransform transform,
            ref MovementAgentComponent move,
            ref MovementSteeringComponent steering,
            [ReadOnly] in MovementAgentAvoidanceComponent avoidance)
        {
            float3 pos = transform.Position;
            var costs = GridEntity != Entity.Null && GridCostsLookup.HasBuffer(GridEntity)
                ? GridCostsLookup[GridEntity].AsNativeArray() : default;

            // --- 1. ANTI-DEADLOCK: Stuck Detection ---
            if (move.hastarget && move.targetResolutionKind != TargetResolutionKind.None)
            {
                // Tính khoảng cách THỰC TẾ di chuyển được so với frame trước
                float actualDistMoved = math.distance(pos.xz, steering.lastPosition.xz);
                float expectedDist = move.speed * DeltaTime;
                
                // Cập nhật lastPosition cho frame tiếp theo
                steering.lastPosition = pos;

                // Nếu đi được < 10% so với lý thuyết (bị chặn vật lý)
                if (actualDistMoved < expectedDist * 0.1f)
                    steering.stuckTime += DeltaTime;
                else
                    steering.stuckTime = 0; // Real XZ progress, including wall sliding, is not a deadlock.

                float distToGlobal = math.distance(pos.xz, move.realTarget.xz);

                bool blockedByNeighbor = avoidance.closestDistance < avoidance.radius * 2.5f
                                         && avoidance.neighborCount > 0;
                // arrivalRadius is a braking range, not permission to discard a command.
                // Keep waiting at bottlenecks and walls. Only settle beside the goal
                // when another unit occupies it and the goal is directly reachable.
                float settleRadius = math.max(steering.stoppingDistance, avoidance.radius * 2);
                if (blockedByNeighbor && distToGlobal <= settleRadius && steering.stuckTime > .5f
                    && GridObstacleAvoidance.ClearSegment(pos.xz, move.realTarget.xz, avoidance.radius, costs, Grid))
                {
                    // Settle bình thường — khi settled, prefVel = 0
                    // → separation trở thành lực duy nhất → overlap tự giải
                    move.hastarget = false;
                    move.stopReason = MovementStopReason.CrowdedDestination;
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
                
                // GIỚI HẠN LỰC ĐẨY: Không cho phép EntityOwner bị teleport đi quá xa trong 1 frame
                // Tối đa trượt đi 1 khoảng bằng 1.5 lần bán kính của nó mỗi frame
                float maxCorrection = avoidance.radius * 1.5f;
                if (math.length(correction) > maxCorrection)
                {
                    correction = math.normalizesafe(correction) * maxCorrection;
                }
                
                correction.y = 0;
                float2 corrected = GridObstacleAvoidance.MoveAndSlide(transform.Position.xz, correction.xz, avoidance.radius, costs, Grid);
                var correctedPosition = transform.Position;
                correctedPosition.x = corrected.x; correctedPosition.z = corrected.y;
                transform.Position = correctedPosition;
            }

            // --- 3. APPLY VELOCITY (từ ORCA) ---
            if (math.lengthsq(move.velocity) > 0.001f)
            {
                float2 before = transform.Position.xz;
                float2 after = GridObstacleAvoidance.MoveAndSlide(before, move.velocity.xz * DeltaTime, avoidance.radius, costs, Grid);
                var nextPosition = transform.Position;
                nextPosition.x = after.x; nextPosition.z = after.y;
                transform.Position = nextPosition;
                float2 actualVelocity = DeltaTime > 0 ? (after - before) / DeltaTime : float2.zero;
                move.velocity = new float3(actualVelocity.x, 0, actualVelocity.y);
                steering.isSettled = math.lengthsq(actualVelocity) < .001f;
            }
            else
            {
                steering.isSettled = true;
            }

            // --- 4. ROTATION ---
            // Separation can move an idle agent even when its final velocity is zero.
            if (math.any(pos.xz != transform.Position.xz) && PendingHeightLookup.HasComponent(entity))
                PendingHeightLookup.SetComponentEnabled(entity, true);

            if (math.lengthsq(move.velocity) > 0.01f)
            {
                float3 moveDir = math.normalizesafe(move.velocity);
                quaternion targetRot = quaternion.LookRotationSafe(moveDir, math.up());
                transform.Rotation = math.slerp(transform.Rotation, targetRot, DeltaTime * steering.rotationSpeed);
            }
            else if (move.FieldEntity != Entity.Null)
            {
                float3 lookDir = move.currentworldtarget - transform.Position;
                lookDir.y = 0;

                if (math.lengthsq(lookDir) > 0.01f)
                {
                    float3 desiredDir = math.normalizesafe(lookDir);
                    float3 forward = math.normalizesafe(transform.Forward());
                    forward.y = 0;
                    forward = math.normalizesafe(forward);

                    float dot = math.clamp(math.dot(forward, desiredDir), -1f, 1f);

                    if (dot < 0.9659258f)
                    {
                        quaternion targetRot = quaternion.LookRotationSafe(desiredDir, math.up());
                        transform.Rotation = math.slerp(transform.Rotation, targetRot, DeltaTime * steering.rotationSpeed);
                    }
                }
            }
        }
    }
}
