using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections.LowLevel.Unsafe;

/// <summary>
/// ORCA System (Clean): Local avoidance khi di chuyển.
/// 
/// Pipeline: TargetSystem (preferredVelocity) → ORCASystem (velocity) → ActuatorSystem (position)
/// 
/// Vai trò DUY NHẤT: Tính velocity collision-free bằng ORCA LP solver.
/// KHÔNG xử lý convergence — đó là việc của stuck system trong ActuatorSystem.
/// 
/// Bổ sung:
///   - Fix 3 (Speed Scaling): Giảm tốc khi nhiều settled neighbors → yield right-of-way
///   - Grid Gradient: Né tường/obstacle trên grid (từ Context Steering cũ)
/// </summary>
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(MoveAgentSpatialSystem))]
[UpdateAfter(typeof(MovementAgentTargetSystem))]
public partial struct MovementAgentORCASystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<GridComponent>()) return;

        var grid = SystemAPI.GetSingleton<GridComponent>();
        var container = SystemAPI.GetSingleton<MovementAgentBucket>();
        var deltaTime = SystemAPI.Time.DeltaTime;
        var gridEntity = SystemAPI.GetSingletonEntity<GridComponent>();

        var job = new ORCAJob
        {
            Grid = grid,
            BucketMap = container.Bucket,
            AvoidanceLookup = SystemAPI.GetComponentLookup<MovementAgentAvoidanceComponent>(true),
            TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
            MoveLookup = SystemAPI.GetComponentLookup<MovementAgentComponent>(true),
            GridCosts = SystemAPI.GetBuffer<GridNodeCost>(gridEntity).AsNativeArray(),
            InvDeltaTime = 1.0f / deltaTime,
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public partial struct ORCAJob : IJobEntity
    {
        [ReadOnly] public GridComponent Grid;
        [ReadOnly] public NativeParallelMultiHashMap<int, Entity> BucketMap;

        [NativeDisableContainerSafetyRestriction][ReadOnly]
        public ComponentLookup<MovementAgentAvoidanceComponent> AvoidanceLookup;

        [NativeDisableContainerSafetyRestriction][ReadOnly]
        public ComponentLookup<LocalTransform> TransformLookup;

        [NativeDisableContainerSafetyRestriction][ReadOnly]
        public ComponentLookup<MovementAgentComponent> MoveLookup;

        [ReadOnly] public NativeArray<GridNodeCost> GridCosts;

        public float InvDeltaTime;

        // --- ORCA Parameters ---
        const int MAX_NEIGHBORS = 25;
        const float TIME_HORIZON = 1.5f;
        const float NEIGHBOR_RANGE = 6.0f;

        public void Execute(Entity entity,
            ref MovementAgentComponent move,
            ref MovementAgentAvoidanceComponent avoidance,
            [ReadOnly] in LocalTransform transform)
        {
            float3 pos = transform.Position;
            float2 agentVel = new float2(move.velocity.x, move.velocity.z);
            float2 prefVel = new float2(move.preferredVelocity.x, move.preferredVelocity.z);
            float maxSpeed = move.speed;
            float agentRadius = avoidance.radius;

            // --- 0. CHECK WALL NORMAL ---
            float2 gridGradient = UnitMovementMath.CalculateGridGradient(
                pos, GridCosts, Grid, agentRadius + 0.8f);
            bool isNearWall = math.lengthsq(gridGradient) > 0.001f;
            float3 wallNormal = isNearWall ? new float3(gridGradient.x, 0, gridGradient.y) : float3.zero;

            // --- 1. SCAN NEIGHBORS ---
            int2 cell = GridHelper.WorldToGrid(pos, Grid);
            float searchRadius = agentRadius + NEIGHBOR_RANGE;
            int searchExtent = math.min((int)math.ceil(searchRadius / Grid.cellsize), 4);

            float closestDist = float.MaxValue;
            float3 closestNormal = float3.zero;
            float3 separationPush = float3.zero;
            int neighborCount = 0;
            int settledCount = 0;

            var lines = new NativeArray<ORCAMath.Line>(MAX_NEIGHBORS, Allocator.Temp);
            int lineCount = 0;

            for (int gx = -searchExtent; gx <= searchExtent && lineCount < MAX_NEIGHBORS; gx++)
            {
                for (int gy = -searchExtent; gy <= searchExtent && lineCount < MAX_NEIGHBORS; gy++)
                {
                    int2 neighborCell = cell + new int2(gx, gy);
                    if (neighborCell.x < 0 || neighborCell.x >= Grid.width ||
                        neighborCell.y < 0 || neighborCell.y >= Grid.height)
                        continue;

                    int neighborIndex = GridHelper.GetNodeIndex(neighborCell, Grid);
                    if (!BucketMap.TryGetFirstValue(neighborIndex, out Entity neighbor, out var it))
                        continue;

                    do
                    {
                        if (neighbor == entity) continue;
                        if (!AvoidanceLookup.HasComponent(neighbor)) continue;
                        if (lineCount >= MAX_NEIGHBORS) break;

                        float3 nPos = TransformLookup[neighbor].Position;
                        float3 diff = pos - nPos;
                        diff.y = 0;
                        float dist = math.length(diff);

                        float neighborRadius = AvoidanceLookup[neighbor].radius;
                        float combinedRadius = agentRadius + neighborRadius + 0.15f;

                        float maxDist = combinedRadius + NEIGHBOR_RANGE;
                        if (dist > maxDist) continue;

                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            closestNormal = dist > 0.001f
                                ? diff / dist
                                : new float3(1, 0, 0);
                        }
                        neighborCount++;

                        if (AvoidanceLookup[neighbor].IsStatic) settledCount++;

                        float actualCombined = agentRadius + neighborRadius;
                        if (dist < actualCombined)
                        {
                            float overlap = 1.0f - dist / actualCombined;
                            float3 pushDir = dist > 0.001f
                                ? diff / dist
                                : new float3(1, 0, 0);

                            if (isNearWall)
                            {
                                float pushIntoWall = math.dot(pushDir, -wallNormal);
                                if (pushIntoWall > 0)
                                {
                                    pushDir -= (-wallNormal) * pushIntoWall;
                                    pushDir += wallNormal * pushIntoWall;
                                }
                            }

                            separationPush += pushDir * overlap;
                        }

                        // --- 2. TẠO ORCA LINE ---
                        float2 relPos = new float2(nPos.x - pos.x, nPos.z - pos.z);
                        float3 nVel3 = MoveLookup.HasComponent(neighbor)
                            ? MoveLookup[neighbor].velocity
                            : float3.zero;
                        float2 neighborVel = new float2(nVel3.x, nVel3.z);

                        bool neighborStatic = AvoidanceLookup[neighbor].IsStatic;
                        float recipFactor = (neighborStatic || isNearWall) ? 1.0f : 0.5f;

                        lines[lineCount] = ORCAMath.CreateAgentLine(
                            relPos, agentVel, neighborVel,
                            combinedRadius, TIME_HORIZON, InvDeltaTime,
                            recipFactor
                        );
                        lineCount++;

                    } while (BucketMap.TryGetNextValue(out neighbor, ref it));
                }
            }

            // --- 3. GRID GRADIENT (wall avoidance) ---
            if (isNearWall)
            {
                separationPush += wallNormal * 0.3f;

                prefVel += gridGradient * maxSpeed * 0.5f;
                if (math.lengthsq(prefVel) > maxSpeed * maxSpeed)
                    prefVel = math.normalize(prefVel) * maxSpeed;
            }

            // --- 4. LP SOLVE ---
            float2 newVel;

            if (lineCount > 0)
            {
                newVel = prefVel;
                int lpResult = ORCAMath.LinearProgram2(lines, lineCount, maxSpeed, prefVel, ref newVel);

                if (lpResult < lineCount)
                {
                    ORCAMath.LinearProgram3(lines, lineCount, lpResult, maxSpeed, ref newVel);
                }
            }
            else
            {
                if (math.lengthsq(prefVel) > maxSpeed * maxSpeed)
                    newVel = math.normalize(prefVel) * maxSpeed;
                else
                    newVel = prefVel;
            }

            // --- 6. OUTPUT ---
            move.velocity = new float3(newVel.x, 0, newVel.y);

            lines.Dispose();

            avoidance.closestDistance = closestDist;
            avoidance.separationForce = separationPush; // Cumulative push từ TẤT CẢ overlapping
            avoidance.neighborCount = neighborCount;
            avoidance.IsStatic = !move.hastarget;
        }
    }
}
