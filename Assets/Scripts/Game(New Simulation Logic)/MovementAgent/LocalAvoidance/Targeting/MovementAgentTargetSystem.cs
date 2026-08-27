using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(FlowDirectionSystem))]
[UpdateAfter(typeof(MovementAgentGroupFormationSystem))]
public partial struct MovementAgentTargetSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var grid = SystemAPI.GetSingleton<GridComponent>();
        var gridEntity = SystemAPI.GetSingletonEntity<GridComponent>();
        var deltaTime = SystemAPI.Time.DeltaTime;
        var job = new UnitTargetJob
        {
            Grid = grid,
            FieldNodeLookup = SystemAPI.GetBufferLookup<FieldNode>(true),
            IslandSeedLookup = SystemAPI.GetBufferLookup<IslandSeed>(true),
            GridIslands = SystemAPI.GetBuffer<GridIsland>(gridEntity).AsNativeArray(),
            GridCosts = SystemAPI.GetBuffer<GridNodeCost>(gridEntity).AsNativeArray(),
            DeltaTime = deltaTime
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public partial struct UnitTargetJob : IJobEntity
    {
        [ReadOnly] public GridComponent Grid;
        [ReadOnly] public BufferLookup<FieldNode> FieldNodeLookup;
        [ReadOnly] public BufferLookup<IslandSeed> IslandSeedLookup;
        [ReadOnly] public NativeArray<GridIsland> GridIslands;
        [ReadOnly] public NativeArray<GridNodeCost> GridCosts;
        [ReadOnly] public float DeltaTime;
        public void Execute(Entity entity, [ReadOnly] in LocalTransform transform, 
            ref MovementAgentComponent move, 
            ref MovementSteeringComponent steering)
        {
            float3 pos = transform.Position;
            float3 globalTarget = move.currentworldtarget;
            float distToGlobal = math.distance(pos, globalTarget);
            float3 islandGoal = globalTarget;

            // Mặc định preferred velocity = zero (ORCA sẽ dùng giá trị này)
            move.preferredVelocity = float3.zero;

            if (!move.hastarget)
            {
                steering.stuckTime = 0;
                steering.minDistanceToTarget = float.MaxValue;
                return;
            }

            if (move.FieldEntity == Entity.Null || !FieldNodeLookup.HasBuffer(move.FieldEntity)) return;
            var buffer = FieldNodeLookup[move.FieldEntity];

            int2 gridPos = GridHelper.WorldToGrid(pos, Grid);
            int nodeIndex = GridHelper.GetNodeIndex(gridPos, Grid);
            int unitIsland = GridIslands[nodeIndex].islandID;

            // --- 1. ISLAND SYNC ---
            int2 globalTargetGrid = GridHelper.WorldToGrid(globalTarget, Grid);
            bool targetIsObstacle = false;
            if (globalTargetGrid.x >= 0 && globalTargetGrid.x < Grid.width && globalTargetGrid.y >= 0 && globalTargetGrid.y < Grid.height)
            {
                int targetNodeIndex = GridHelper.GetNodeIndex(globalTargetGrid, Grid);
                targetIsObstacle = GridCosts[targetNodeIndex].cost >= 250;
            }
            
            if (IslandSeedLookup.HasBuffer(move.FieldEntity) && !targetIsObstacle)
            {
                var seedBuffer = IslandSeedLookup[move.FieldEntity];
                float minDistToSeed = float.MaxValue;
                
                for (int i = 0; i < seedBuffer.Length; i++)
                {
                    if (seedBuffer[i].islandID == unitIsland)
                    {
                        float dSq = math.distancesq(pos, seedBuffer[i].seedPosition);
                        if (dSq < minDistToSeed)
                        {
                            minDistToSeed = dSq;
                            islandGoal = seedBuffer[i].seedPosition;
                        }
                    }
                }
            }

            move.realTarget = islandGoal;
            float3 finalGoal = move.realTarget;

            // --- 2. SLOT FORMATION ---
            if (math.lengthsq(move.slotTarget) > 0.001f && distToGlobal < steering.formationRange)
            {
                // Kiểm tra xem slot còn hợp lệ không (có thể địa hình vừa bị thay đổi như xây nhà)
                int2 slotCell = GridHelper.WorldToGrid(move.slotTarget, Grid);
                bool isSlotValid = true;
                if (slotCell.x >= 0 && slotCell.x < Grid.width && slotCell.y >= 0 && slotCell.y < Grid.height)
                {
                    int slotNodeIndex = GridHelper.GetNodeIndex(slotCell, Grid);
                    if (GridCosts[slotNodeIndex].cost >= 250) isSlotValid = false;
                }
                else isSlotValid = false;

                if (!isSlotValid)
                {
                    move.slotTarget = float3.zero;
                    move.useSlotTarget = false;
                }
                else
                {
                    int currentCellcost = buffer[nodeIndex].bestcost;
                    float directDistToSlot = math.distance(pos, move.slotTarget);

                    float2 flowDir = buffer[nodeIndex].direction;
                    float2 toSlotDir = math.normalizesafe(new float2(
                        move.slotTarget.x - pos.x,
                        move.slotTarget.z - pos.z
                    ));
                    float dotFlowSlot = math.dot(flowDir, toSlotDir);

                    bool slotAlongPath = dotFlowSlot > -0.2f || directDistToSlot < steering.stoppingDistance * 3f;

                    if (slotAlongPath)
                    {
                        float pathDist = currentCellcost / 10f;
                        if (currentCellcost == int.MaxValue || pathDist <= directDistToSlot * 1.8f)
                        {
                            finalGoal = move.slotTarget;
                            if (distToGlobal < steering.arrivalRadius) move.useSlotTarget = true;
                        }
                    }
                }
            }

            float distToFinal = math.distance(pos, finalGoal);

            // --- 3. ARRIVAL CHECK ---
            if (distToFinal < steering.stoppingDistance)
            {
                move.hastarget = false;
                steering.isSettled = true;
                return;
            }

            // --- 4. CALCULATE DESIRED VELOCITY ---
            float3 flowVelocity = UnitMovementMath.CalculateFlowVelocity(
                pos, buffer.AsNativeArray(), Grid.origin, Grid.cellsize, Grid.width, Grid.height, move.speed
            );

            float3 directDir = finalGoal - pos;
            directDir.y = 0;
            float distToFinalGoal = math.length(directDir);
            float3 directVelocity = distToFinalGoal > 0.001f ? (directDir / distToFinalGoal) * move.speed : float3.zero;

            float targetWeight = math.clamp(1.0f - (distToFinalGoal / steering.formationRange), 0f, 1f);
            if (distToFinalGoal < Grid.cellsize * 2f) targetWeight = math.max(targetWeight, 0.5f);

            move.preferredVelocity = math.lerp(flowVelocity, directVelocity, targetWeight);

            // --- 5. ARRIVAL DAMPING ---
            if (distToFinalGoal < steering.arrivalRadius)
            {
                float speedMultiplier = math.clamp(distToFinalGoal / steering.arrivalRadius, 0.1f, 1.0f);
                move.preferredVelocity *= speedMultiplier;
            }

            // --- 6. ANTI-DEADLOCK TRACKING ---
            //stuckscale theo speed
            if (steering.minDistanceToTarget <= 0) steering.minDistanceToTarget = float.MaxValue;
            if (distToFinal < steering.minDistanceToTarget - DeltaTime*move.speed*0.6f)
            {
                steering.minDistanceToTarget = distToFinal;
                steering.stuckTime = 0; 
            }
            // Chú ý: stuckTime sẽ được tăng lên trong ActuatorSystem dựa trên DeltaTime
        }
    }
}
