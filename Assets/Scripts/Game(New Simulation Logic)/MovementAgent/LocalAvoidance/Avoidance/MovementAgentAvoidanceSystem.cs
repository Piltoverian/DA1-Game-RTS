using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections.LowLevel.Unsafe;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(UnitSpatialSystem))]
[UpdateAfter(typeof(MovementAgentGroupFormationSystem))]
public partial struct MovementAgentAvoidanceSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var grid = SystemAPI.GetSingleton<GridComponent>();
        var container = SystemAPI.GetSingleton<MovementAgentBucket>();
        var deltaTime = SystemAPI.Time.DeltaTime;

        var gridEntity = SystemAPI.GetSingletonEntity<GridComponent>();
        var job = new LocalAvoidanceJob
        {
            Grid = grid,
            BucketMap = container.Bucket,
            AvoidanceLookup = SystemAPI.GetComponentLookup<MovementAgentAvoidanceComponent>(true),
            TransformLookup = SystemAPI.GetComponentLookup<Unity.Transforms.LocalTransform>(true),
            MoveLookup = SystemAPI.GetComponentLookup<MovementAgentComponent>(true),
            GridCosts = SystemAPI.GetBuffer<GridNodeCost>(gridEntity).AsNativeArray(),
            DeltaTime = deltaTime
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
    }


    [BurstCompile]
    public partial struct LocalAvoidanceJob : IJobEntity
    {
        [ReadOnly] public GridComponent Grid;
        [ReadOnly] public NativeParallelMultiHashMap<int, Entity> BucketMap;
        [NativeDisableContainerSafetyRestriction] [ReadOnly] public ComponentLookup<MovementAgentAvoidanceComponent> AvoidanceLookup;
        [NativeDisableContainerSafetyRestriction] [ReadOnly] public ComponentLookup<Unity.Transforms.LocalTransform> TransformLookup;
        [ReadOnly] public ComponentLookup<MovementAgentComponent> MoveLookup;
        [ReadOnly] public NativeArray<GridNodeCost> GridCosts;
        public float DeltaTime;

        public void Execute(Entity entity, 
            ref MovementAgentAvoidanceComponent avoidance, 
            ref DynamicBuffer<ContextMapElement> contextMap,
            ref DynamicBuffer<ContextHistoryElement> contextHistory,
            [ReadOnly] in ContextSteeringConfig config,
            [ReadOnly] in Unity.Transforms.LocalTransform transform,
            [ReadOnly] in MovementSteeringComponent steering) // Thêm ReadOnly component ở đây
        {
            int res = config.Resolution;
            float3 pos = transform.Position;
            
            // 1. CLEAR & INITIALIZE (Stateless)
            for (int i = 0; i < res; i++)
            {
                contextMap[i] = new ContextMapElement { Interest = 0f, Danger = 0f };
            }

            // --- STATIC CHECK & INTEREST ---
            float distToGlobal = 0f;
            if (MoveLookup.TryGetComponent(entity, out var move))
            {
                avoidance.IsStatic = !move.hastarget;

                if (move.hastarget)
                {
                    distToGlobal = math.distance(pos, move.currentworldtarget);

                    // CHỈ quan tâm đến Slot khi đã vào Range, nếu không thì hướng về đích thực tế của đảo (realTarget)
                    float3 targetPos = (math.lengthsq(move.slotTarget) > 0.001f && distToGlobal < steering.formationRange) 
                        ? move.slotTarget : move.realTarget;
                    
                    float3 targetDir = math.normalize(targetPos - pos);
                    targetDir.y = 0;
                    
                    for (int i = 0; i < res; i++)
                    {
                        float angle = i * (2f * math.PI / res);
                        float3 slotDir = new float3(math.cos(angle), 0, math.sin(angle));
                        float dot = math.dot(slotDir, targetDir);
                        contextMap[i] = new ContextMapElement { 
                            Interest = math.max(0, dot), 
                            Danger = 0f 
                        };
                    }
                }
            }
            else
            {
                avoidance.IsStatic = true; // Nếu không có move component thì mặc định là vật cản tĩnh
            }

            if (avoidance.IsStatic)
            {
                avoidance.avoidanceForce = float3.zero;
                avoidance.lastAvoidDir = float3.zero; // QUAN TRỌNG: Dừng việc tự né tránh khi đã đến đích
            }

            // 3. EVALUATE DANGER & SEPARATION
            int2 cell = GridHelper.WorldToGrid(pos, Grid);
            float avoidRadius = avoidance.radius + 2.0f; // Bán kính nhìn xa cơ bản để dùng cho Broad-phase và Né tường
            float contactZone = avoidance.radius + 0.5f; 
            float separationZone = avoidance.radius * 0.9f; 
            
            float3 sepForce = float3.zero;
            float closestDist = float.MaxValue;
            int neighborCount = 0;

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    int2 neighborCell = cell + new int2(x, y);
                    if (neighborCell.x < 0 || neighborCell.x >= Grid.width || neighborCell.y < 0 || neighborCell.y >= Grid.height) continue;

                    int neighborIndex = GridHelper.GetNodeIndex(neighborCell, Grid);
                    if (BucketMap.TryGetFirstValue(neighborIndex, out Entity neighbor, out var it))
                    {
                        do
                        {
                            if (neighbor == entity) continue;
                            if (!AvoidanceLookup.HasComponent(neighbor)) continue;

                            float3 neighborPos = TransformLookup[neighbor].Position;
                            float3 diff = pos - neighborPos;
                            diff.y = 0;
                            float dist = math.length(diff);

                             // --- DYNAMIC SUM-OF-RADII (Sử dụng UnitMovementMath) ---
                            UnitMovementMath.CalculateSumOfRadii(
                                avoidance.radius, 
                                AvoidanceLookup[neighbor].radius, 
                                out float localSeparationZone, 
                                out float localContactZone, 
                                out float localAvoidRadius
                            );

                            if (dist > 0.001f && dist < localAvoidRadius)
                            {
                                if (dist < closestDist) closestDist = dist;
                                neighborCount++;

                                float3 normal = diff / math.max(dist, 0.1f);
                                
                                // --- HARD SEPARATION ---
                                if (dist < localSeparationZone)
                                {
                                    sepForce += normal * (localSeparationZone - dist) / localSeparationZone;
                                }

                                // --- CONTEXT DANGER (Sử dụng UnitMovementMath) ---
                                float3 myDir = math.normalizesafe(MoveLookup[entity].velocity);
                                float3 neighborDir = math.normalizesafe(MoveLookup[neighbor].velocity);
                                float consensus = math.dot(myDir, neighborDir);

                                for (int i = 0; i < res; i++)
                                {
                                    float angle = i * (2f * math.PI / res);
                                    float3 slotDir = new float3(math.cos(angle), 0, math.sin(angle));
                                    float dot = math.dot(slotDir, -normal); 

                                    if (dot > math.cos(2f*math.PI/res)) // Chỉ tính danger cho các slot hướng về sát đối phương
                                    {
                                        float staticMultiplier = AvoidanceLookup[neighbor].IsStatic ? 1.2f : 1.0f;
                                        float dangerValue = UnitMovementMath.CalculateDanger(
                                            dist, localAvoidRadius, localContactZone, dot, staticMultiplier, 
                                            MoveLookup[entity].velocity, MoveLookup[neighbor].velocity
                                        );

                                        var mapItem = contextMap[i];
                                        mapItem.Danger = math.max(mapItem.Danger, dangerValue); 
                                        contextMap[i] = mapItem;
                                    }
                                }
                            }
                        } while (BucketMap.TryGetNextValue(out neighbor, ref it));
                    }
                }
            }

            // --- 3.5 GRID OBSTACLE DANGER (Obstacle Gradient Avoidance) ---
            // Thay vì binary check, chúng ta dùng Grid Gradient để cảm nhận "vùng nguy hiểm" mượt mà quanh tường
            float2 gridGradient = UnitMovementMath.CalculateGridGradient(pos, GridCosts, Grid, avoidRadius);
            float3 gradDir3D = new float3(gridGradient.x, 0, gridGradient.y);

            for (int i = 0; i < res; i++)
            {
                float angle = i * (2f * math.PI / res);
                float3 slotDir = new float3(math.cos(angle), 0, math.sin(angle));
                
                // Nếu hướng slotDir đâm thẳng vào tường (ngược hướng gradient), tăng Danger
                float wallAlignment = math.dot(slotDir, -gradDir3D);
                if (wallAlignment > 0f)
                {
                    var mapItem = contextMap[i];
                    // Gradient danger tỉ lệ với mức độ đâm thẳng vào tường
                    mapItem.Danger = math.max(mapItem.Danger, wallAlignment); 
                    contextMap[i] = mapItem;
                }
            }

            // 4. RESOLVE (Solver Engine)
            int bestSlot = -1;
            float maxInterest = -1f;

            for (int i = 0; i < res; i++)
            {
                var mapItem = contextMap[i];
                
                // Danger Masking
                if (mapItem.Danger > config.DangerThreshold) mapItem.Interest = 0f;
                else mapItem.Interest *= (1.0f - mapItem.Danger); // Weighted avoidance

                // Temporal Hysteresis (EMA) to suppress oscillations
                // Khi ở gần đích (trong range), chúng ta tăng độ mượt (alpha nhỏ hơn) để tránh đổi hướng liên tục
                float currentAlpha = distToGlobal < steering.formationRange ? 0.2f : config.H_Alpha;
                mapItem.Interest = math.lerp(contextHistory[i].LastInterest, mapItem.Interest, currentAlpha);
                contextHistory[i] = new ContextHistoryElement { LastInterest = mapItem.Interest };

                if (mapItem.Interest > maxInterest)
                {
                    maxInterest = mapItem.Interest;
                    bestSlot = i;
                }
                contextMap[i] = mapItem;
            }

            // 5. QUADRATIC INTERPOLATION & ACTUATION
            if (bestSlot != -1 && maxInterest > 0.01f)
            {
                int prev = (bestSlot - 1 + res) % res;
                int next = (bestSlot + 1) % res;

                float vM = contextMap[prev].Interest;
                float vC = contextMap[bestSlot].Interest;
                float vP = contextMap[next].Interest;

                float offset = UnitMovementMath.CalculateQuadraticOffset(vM, vC, vP);
                float finalAngle = (bestSlot + offset) * (2f * math.PI / res);

                avoidance.lastAvoidDir = new float3(math.cos(finalAngle), 0, math.sin(finalAngle));
                avoidance.avoidTimer = 0.2f; 
            }

            avoidance.separationForce = sepForce;
            avoidance.neighborCount = neighborCount;
            avoidance.closestDistance = closestDist;

        }
    }
}


