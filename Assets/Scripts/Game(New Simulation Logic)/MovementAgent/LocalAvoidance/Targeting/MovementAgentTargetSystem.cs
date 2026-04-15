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

        var job = new UnitTargetJob
        {
            Grid = grid,
            FieldNodeLookup = SystemAPI.GetBufferLookup<FieldNode>(true),
            IslandSeedLookup = SystemAPI.GetBufferLookup<IslandSeed>(true),
            GridIslands = SystemAPI.GetBuffer<GridIsland>(gridEntity).AsNativeArray()
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
            if (IslandSeedLookup.HasBuffer(move.FieldEntity))
            {
                var seedBuffer = IslandSeedLookup[move.FieldEntity];
                for (int i = 0; i < seedBuffer.Length; i++)
                {
                    if (seedBuffer[i].islandID == unitIsland)
                    {
                        islandGoal = seedBuffer[i].seedPosition;
                        break;
                    }
                }
            }
            move.realTarget = islandGoal;
            float3 finalGoal = move.realTarget;

            // --- 2. SLOT FORMATION ---
            if (math.lengthsq(move.slotTarget) > 0.001f && distToGlobal < steering.formationRange)
            {
                int currentCellcost = buffer[nodeIndex].bestcost;
                float pathDist = currentCellcost / 10f;
                float directDistToSlot = math.distance(pos, move.slotTarget);

                if (currentCellcost == int.MaxValue || pathDist <= directDistToSlot * 1.8f)
                {
                    finalGoal = move.slotTarget;
                    if (distToGlobal < steering.arrivalRadius) move.useSlotTarget = true;
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
            // Progress threshold = 0.1 — lọc oscillation nhỏ (0.01/frame) nhưng nhận progress thật
            // speed=10, dt=0.02 → move 0.2/frame → 0.1 = nửa frame progress = hợp lý
            // 0.01 cũ quá nhỏ → agent đẩy 0.01/frame qua crowd → reset timer → không bao giờ stuck!
            if (steering.minDistanceToTarget <= 0) steering.minDistanceToTarget = float.MaxValue;
            if (distToFinal < steering.minDistanceToTarget - 0.1f)
            {
                steering.minDistanceToTarget = distToFinal;
                steering.stuckTime = 0; 
            }
            // Chú ý: stuckTime sẽ được tăng lên trong ActuatorSystem dựa trên DeltaTime
        }
    }
}
