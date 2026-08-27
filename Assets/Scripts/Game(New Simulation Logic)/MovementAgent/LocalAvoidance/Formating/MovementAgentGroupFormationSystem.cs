using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(IntegrationFieldSystem))]
public partial struct MovementAgentGroupFormationSystem : ISystem
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
        
        // 1. Thu thập tất cả các entity đang nhận lệnh TargetChangeRequest
        var requestQuery = SystemAPI.QueryBuilder().WithAll<TargetChangeRequest, MovementAgentComponent, MovementAgentAvoidanceComponent, MovementAgentFormationComponent>().Build();
        if (requestQuery.IsEmpty) return;

        var entities = requestQuery.ToEntityArray(Allocator.Temp);
        var requests = requestQuery.ToComponentDataArray<TargetChangeRequest>(Allocator.Temp);
        
        // Nhóm entity theo tọa độ đích (Quantized target)
        var targetGroups = new NativeParallelMultiHashMap<int3, Entity>(entities.Length, Allocator.Temp);
        var uniqueTargets = new NativeParallelHashSet<int3>(entities.Length, Allocator.Temp);
        
        for (int i = 0; i < entities.Length; i++)
        {
            int3 quantizedTarget = new int3((int)math.round(requests[i].newWorldTarget.x * 100), 
                                           (int)math.round(requests[i].newWorldTarget.y * 100), 
                                           (int)math.round(requests[i].newWorldTarget.z * 100));
            targetGroups.Add(quantizedTarget, entities[i]);
            uniqueTargets.Add(quantizedTarget);
        }
        
        if (targetGroups.IsEmpty)
        {
            entities.Dispose();
            requests.Dispose();
            targetGroups.Dispose();
            uniqueTargets.Dispose();
            return;
        }

        var uniqueKeyArray = uniqueTargets.ToNativeArray(Allocator.Temp);
        var gridEntity = SystemAPI.GetSingletonEntity<GridComponent>();
        var islandBuffer = SystemAPI.GetBuffer<GridIsland>(gridEntity).AsNativeArray();
        var costBuffer = SystemAPI.GetBuffer<GridNodeCost>(gridEntity).AsNativeArray();

        for (int k = 0; k < uniqueKeyArray.Length; k++)
        {
            var targetKey = uniqueKeyArray[k];
            NativeList<Entity> groupEntities = new NativeList<Entity>(Allocator.Temp);
            float maxRadius = 0.5f;
            FormationType groupFormation = FormationType.Box;

            var it = targetGroups.GetValuesForKey(targetKey);
            while (it.MoveNext())
            {
                Entity e = it.Current;
                groupEntities.Add(e);
                var avoidance = SystemAPI.GetComponent<MovementAgentAvoidanceComponent>(e);
                if (avoidance.radius > maxRadius) maxRadius = avoidance.radius;
                var form = SystemAPI.GetComponent<MovementAgentFormationComponent>(e);
                if (form.SelectedType == FormationType.Circle) groupFormation = FormationType.Circle;
            }

            if (groupEntities.Length == 0) { groupEntities.Dispose(); continue; }

            float safeDistance = maxRadius * 2.0f * 1.1f;
            float3 baseTarget = SystemAPI.GetComponent<TargetChangeRequest>(groupEntities[0]).newWorldTarget;

            // --- PHÂN BỔ ĐỘI HÌNH ĐA ĐẢO (Multi-Island Clustering) ---
            var unitsByIsland = new NativeParallelMultiHashMap<int, Entity>(groupEntities.Length, Allocator.Temp);
            var uniqueIslandsInGroup = new NativeParallelHashSet<int>(groupEntities.Length, Allocator.Temp);

            for (int i = 0; i < groupEntities.Length; i++)
            {
                float3 uPos = SystemAPI.GetComponent<LocalTransform>(groupEntities[i]).Position;
                int uIsland = islandBuffer[GridHelper.GetNodeIndex(GridHelper.WorldToGrid(uPos, grid), grid)].islandID;
                unitsByIsland.Add(uIsland, groupEntities[i]);
                uniqueIslandsInGroup.Add(uIsland);
            }

            // Tìm FlowField cho cụm này
            DynamicBuffer<IslandSeed> seedBuffer = default;
            bool hasSeeds = false;
            var fieldQuery = SystemAPI.QueryBuilder().WithAll<FlowField, IslandSeed>().Build();
            var fieldEntities = fieldQuery.ToEntityArray(Allocator.Temp);
            var fieldData = fieldQuery.ToComponentDataArray<FlowField>(Allocator.Temp);
            int2 gridTarget = GridHelper.WorldToGrid(baseTarget, grid);

            for (int f = 0; f < fieldData.Length; f++)
            {
                if (fieldData[f].targetcell.Equals(gridTarget))
                {
                    seedBuffer = SystemAPI.GetBuffer<IslandSeed>(fieldEntities[f]);
                    hasSeeds = true;
                    break;
                }
            }

            var islandsInGroup = uniqueIslandsInGroup.ToNativeArray(Allocator.Temp);
            for (int iIdx = 0; iIdx < islandsInGroup.Length; iIdx++)
            {
                int currentIslandID = islandsInGroup[iIdx];
                int unitInIslandCount = 0;
                var itC = unitsByIsland.GetValuesForKey(currentIslandID);
                while (itC.MoveNext()) unitInIslandCount++;

                float3 clusterCenter = baseTarget; 
                if (hasSeeds)
                {
                    for (int s = 0; s < seedBuffer.Length; s++)
                    {
                        if (seedBuffer[s].islandID == currentIslandID)
                        { clusterCenter = seedBuffer[s].seedPosition; break; }
                    }
                }

                float3 averageUnitPos = float3.zero;
                var itAssign = unitsByIsland.GetValuesForKey(currentIslandID);
                if (itAssign.MoveNext()) { 
                    averageUnitPos = SystemAPI.GetComponent<LocalTransform>(itAssign.Current).Position;
                }
                
                // Calculate average unit position for the group (or just use the first unit's pos if count=1)
                int avgCount = 1;
                while (itAssign.MoveNext()) {
                    averageUnitPos += SystemAPI.GetComponent<LocalTransform>(itAssign.Current).Position;
                    avgCount++;
                }
                averageUnitPos /= avgCount;

                // Nếu click vào chướng ngại vật (ví dụ: Building), không dùng đội hình cứng (Slot)
                // Thay vào đó, xóa SlotTarget để ORCA và Agent tự bâu vào mục tiêu từ hướng tiếp cận tự nhiên của chúng.
                int2 baseGridPos = GridHelper.WorldToGrid(baseTarget, grid);
                bool isTargetObstacle = false;
                if (baseGridPos.x >= 0 && baseGridPos.x < grid.width && baseGridPos.y >= 0 && baseGridPos.y < grid.height)
                {
                    if (costBuffer[GridHelper.GetNodeIndex(baseGridPos, grid)].cost >= 250)
                        isTargetObstacle = true;
                }

                NativeList<float3> clusterSlots = new NativeList<float3>(Allocator.Temp);
                
                if (!isTargetObstacle)
                {
                    if (groupFormation == FormationType.Circle)
                        FindCircleSlotsWorld(clusterCenter, unitInIslandCount, safeDistance, grid, costBuffer, islandBuffer, currentIslandID, ref clusterSlots, averageUnitPos);
                    else
                        FindBoxSlotsWorld(clusterCenter, unitInIslandCount, safeDistance, grid, costBuffer, islandBuffer, currentIslandID, ref clusterSlots, averageUnitPos);
                }

                // 1. Thu thập tất cả Unit của Island này vào mảng
                NativeList<Entity> islandUnits = new NativeList<Entity>(unitInIslandCount, Allocator.Temp);
                var itAssign2 = unitsByIsland.GetValuesForKey(currentIslandID);
                while (itAssign2.MoveNext()) islandUnits.Add(itAssign2.Current);

                if (isTargetObstacle)
                {
                    // Tự động phân luồng: Xóa slotTarget để lính tự bu vào viền building theo vị trí của chúng
                    for (int u = 0; u < islandUnits.Length; u++)
                    {
                        Entity unitE = islandUnits[u];
                        var move = SystemAPI.GetComponentRW<MovementAgentComponent>(unitE);
                        move.ValueRW.slotTarget = float3.zero;
                        move.ValueRW.useSlotTarget = false;
                        move.ValueRW.lookAtPoint = float3.zero;
                        SystemAPI.GetComponentRW<MovementAgentAvoidanceComponent>(unitE).ValueRW.IsStatic = false;
                    }
                    islandUnits.Dispose();
                    clusterSlots.Dispose();
                    continue;
                }

                NativeList<bool> unitAssigned = new NativeList<bool>(islandUnits.Length, Allocator.Temp);
                for (int j = 0; j < islandUnits.Length; j++) unitAssigned.Add(false);

                // 2. SLOT CHỌN UNIT: Duyệt qua từng Slot (đã được sinh từ tâm ra ngoài)
                for (int s = 0; s < clusterSlots.Length; s++)
                {
                    float3 slotPos = clusterSlots[s];
                    int bestUnitIdx = -1; 
                    float minDist = float.MaxValue;
                    
                    // Tìm Unit gần với Slot này nhất
                    for (int u = 0; u < islandUnits.Length; u++)
                    {
                        if (unitAssigned[u]) continue;
                        float3 unitPos = SystemAPI.GetComponent<LocalTransform>(islandUnits[u]).Position;
                        float d = math.distancesq(unitPos, slotPos);
                        if (d < minDist) { minDist = d; bestUnitIdx = u; }
                    }

                    if (bestUnitIdx != -1)
                    {
                        unitAssigned[bestUnitIdx] = true;
                        Entity unitE = islandUnits[bestUnitIdx];
                        float3 finalSlotPos = slotPos;
                        var move = SystemAPI.GetComponentRW<MovementAgentComponent>(unitE);
                        move.ValueRW.slotTarget = finalSlotPos;
                        move.ValueRW.useSlotTarget = false;
                        
                        float3 moveDir = math.normalizesafe(baseTarget - clusterCenter);
                        if (math.lengthsq(moveDir) < 0.001f) moveDir = math.normalizesafe(clusterCenter - SystemAPI.GetComponent<LocalTransform>(unitE).Position);
                        if (math.lengthsq(moveDir) > 0.001f) move.ValueRW.lookAtPoint = finalSlotPos + moveDir * 5f;
                        else move.ValueRW.lookAtPoint = float3.zero;

                        SystemAPI.GetComponentRW<MovementAgentAvoidanceComponent>(unitE).ValueRW.IsStatic = false;
                    }
                }
                
                islandUnits.Dispose();
                unitAssigned.Dispose();
                clusterSlots.Dispose();
            }

            islandsInGroup.Dispose();
            unitsByIsland.Dispose();
            uniqueIslandsInGroup.Dispose();
            fieldEntities.Dispose();
            fieldData.Dispose();
            groupEntities.Dispose();
        }

        entities.Dispose();
        requests.Dispose();
        targetGroups.Dispose();
        uniqueTargets.Dispose();
        uniqueKeyArray.Dispose();
    }

    private void FindBoxSlotsWorld(float3 center, int count, float spacing, GridComponent grid, 
        NativeArray<GridNodeCost> costs, NativeArray<GridIsland> islands, int targetIsland, ref NativeList<float3> results, float3 approachPos)
    {
        int layer = 0; int found = 0;
        while (found < count && layer < 25)
        {
            NativeList<float3> layerCandidates = new NativeList<float3>(Allocator.Temp);
            for (int x = -layer; x <= layer; x++) {
                for (int y = -layer; y <= layer; y++) {
                    if (math.abs(x) == layer || math.abs(y) == layer) {
                        float3 candidate = center + new float3(x, 0, y) * spacing;
                        if (IsValidSlot(candidate, grid, costs, islands, targetIsland)) {
                            layerCandidates.Add(candidate);
                        }
                    }
                }
            }
            if (layerCandidates.Length > 0)
            {
                // Sắp xếp các ứng viên trong layer theo khoảng cách tới approachPos
                for (int i = 0; i < layerCandidates.Length - 1; i++) {
                    for (int j = i + 1; j < layerCandidates.Length; j++) {
                        if (math.distancesq(layerCandidates[j], approachPos) < math.distancesq(layerCandidates[i], approachPos)) {
                            float3 temp = layerCandidates[i];
                            layerCandidates[i] = layerCandidates[j];
                            layerCandidates[j] = temp;
                        }
                    }
                }
                
                // Lấy các ứng viên gần nhất theo thứ tự
                for (int i = 0; i < layerCandidates.Length; i++) {
                    results.Add(layerCandidates[i]);
                    found++;
                    if (found >= count) break;
                }
            }
            layerCandidates.Dispose();
            if (found >= count) return;
            layer++;
        }
    }

    private void FindCircleSlotsWorld(float3 center, int count, float spacing, GridComponent grid, 
        NativeArray<GridNodeCost> costs, NativeArray<GridIsland> islands, int targetIsland, ref NativeList<float3> results, float3 approachPos)
    {
        if (IsValidSlot(center, grid, costs, islands, targetIsland)) {
            results.Add(center); if (count == 1) return;
        }
        int found = results.Length; float currentRadius = spacing;
        while (found < count && currentRadius < 100f)
        {
            int slotsOnRing = (int)math.floor((2 * math.PI * currentRadius) / spacing);
            if (slotsOnRing < 1) slotsOnRing = 1;
            
            NativeList<float3> layerCandidates = new NativeList<float3>(Allocator.Temp);
            for (int i = 0; i < slotsOnRing; i++) {
                float angle = i * (2 * math.PI / slotsOnRing);
                float3 candidate = center + new float3(math.cos(angle), 0, math.sin(angle)) * currentRadius;
                if (IsValidSlot(candidate, grid, costs, islands, targetIsland)) {
                    layerCandidates.Add(candidate);
                }
            }
            
            if (layerCandidates.Length > 0)
            {
                // Sắp xếp các ứng viên trong layer theo khoảng cách tới approachPos
                for (int i = 0; i < layerCandidates.Length - 1; i++) {
                    for (int j = i + 1; j < layerCandidates.Length; j++) {
                        if (math.distancesq(layerCandidates[j], approachPos) < math.distancesq(layerCandidates[i], approachPos)) {
                            float3 temp = layerCandidates[i];
                            layerCandidates[i] = layerCandidates[j];
                            layerCandidates[j] = temp;
                        }
                    }
                }
                
                // Lấy các ứng viên gần nhất theo thứ tự
                for (int i = 0; i < layerCandidates.Length; i++) {
                    results.Add(layerCandidates[i]);
                    found++;
                    if (found >= count) break;
                }
            }
            layerCandidates.Dispose();
            if (found >= count) return;
            currentRadius += spacing;
        }
    }

    private bool IsValidSlot(float3 pos, GridComponent grid, NativeArray<GridNodeCost> costs, NativeArray<GridIsland> islands, int targetIsland)
    {
        int2 gp = GridHelper.WorldToGrid(pos, grid);
        if (gp.x < 0 || gp.x >= grid.width || gp.y < 0 || gp.y >= grid.height) return false;
        int idx = GridHelper.GetNodeIndex(gp, grid);
        if (costs[idx].cost >= 250) return false; 
        if (islands[idx].islandID != targetIsland) return false;
        return true;
    }
}
