using System;
using System.Collections;
using System.Collections.Generic;
using Unity;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using Unity.VisualScripting;
using UnityEditor.Build.Pipeline;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;


[UpdateAfter(typeof(SelectableSpatialSystem))]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
partial struct SelectSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SelectableBucketContainer>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (request, entity) in
                 SystemAPI.Query<RefRO<SelectionRequest>>().WithEntityAccess())
        {
            switch (request.ValueRO.mode)
            {
                case SelectionMode.Click:
                    HandleClick(request.ValueRO, ref state, ref ecb);
                    break;

                case SelectionMode.Drag:
                    HandleDrag(request.ValueRO, ref state, ref ecb);
                    break;
            }

            ecb.RemoveComponent<SelectionRequest>(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    private void HandleDrag(
        SelectionRequest request,
        ref SystemState state,
        ref EntityCommandBuffer ecb)
    {
        var grid = SystemAPI.GetSingleton<GridComponent>();
        var bucketContainer = SystemAPI.GetSingletonRW<SelectableBucketContainer>();

        var bucket = bucketContainer.ValueRW.Bucket;

        var transformLookup = state.GetComponentLookup<Unity.Transforms.LocalTransform>(true);
        var dragSelectableLookup = state.GetComponentLookup<DragSelectableEntity>(true);
        var selectableLookup = state.GetComponentLookup<Selectable>(true);
        var selectedLookup = state.GetComponentLookup<SelectedTag>(true);

        Trapezoid trapezoid = new Trapezoid(
            request.v1,
            request.v2,
            request.v3,
            request.v4
        );

        float cellSize = grid.cellsize;

        int minRow = (int)((trapezoid.MinZ - grid.origin.z) / cellSize);
        int maxRow = (int)((trapezoid.MaxZ - grid.origin.z) / cellSize);

        minRow = math.clamp(minRow, 0, grid.height - 1);
        maxRow = math.clamp(maxRow, 0, grid.height - 1);

        for (int row = minRow; row <= maxRow; row++)
        {
            float worldZ = grid.origin.z + row * cellSize + cellSize * 0.5f;

            if (!trapezoid.ContainsZ(worldZ))
                continue;

            float leftX = trapezoid.LeftX(worldZ);
            float rightX = trapezoid.RightX(worldZ);

            if (leftX > rightX)
            {
                float tmp = leftX;
                leftX = rightX;
                rightX = tmp;
            }

            int minCol = (int)((leftX - grid.origin.x) / cellSize);
            int maxCol = (int)((rightX - grid.origin.x) / cellSize);

            minCol = math.clamp(minCol, 0, grid.width - 1);
            maxCol = math.clamp(maxCol, 0, grid.width - 1);

            for (int col = minCol; col <= maxCol; col++)
            {
                int cellIndex = row * grid.width + col;

                if (bucket.TryGetFirstValue(cellIndex, out Entity unit, out var it))
                {
                    do
                    {
                        if (!dragSelectableLookup.HasComponent(unit))
                            continue;

                        if (!selectableLookup.HasComponent(unit))
                            continue;

                        float3 pos = transformLookup[unit].Position;

                        if (pos.z < trapezoid.MinZ || pos.z > trapezoid.MaxZ)
                            continue;

                        float lx = trapezoid.LeftX(pos.z);
                        float rx = trapezoid.RightX(pos.z);

                        float minX = math.min(lx, rx);
                        float maxX = math.max(lx, rx);

                        if (pos.x >= minX && pos.x <= maxX)
                        {
                            var selectable = selectableLookup[unit];
                            if (selectable.playerID != request.playerId)
                                continue;

                            if (!selectedLookup.HasComponent(unit))
                                ecb.AddComponent(unit, new SelectedTag { playerID = request.playerId });
                        }

                    } while (bucket.TryGetNextValue(out unit, ref it));
                }
            }
        }
    }

    private void HandleClick(
        SelectionRequest request,
        ref SystemState state,
        ref EntityCommandBuffer ecb)
    {
        foreach (var (_, entity) in
                 SystemAPI.Query<RefRO<SelectedTag>>().WithEntityAccess())
        {
            ecb.RemoveComponent<SelectedTag>(entity);
        }
        var grid = SystemAPI.GetSingleton<GridComponent>();
        var bucketContainer = SystemAPI.GetSingletonRW<SelectableBucketContainer>();

        var bucket = bucketContainer.ValueRW.Bucket;
        var selectableLookup = state.GetComponentLookup<Selectable>(true);
        var selectedLookup = state.GetComponentLookup<SelectedTag>(true);
        var transformLookup = state.GetComponentLookup<Unity.Transforms.LocalTransform>(true);
        var singleselectableLookup = state.GetComponentLookup<SingleSelectableEntity>(true);
        var dragSelectableLookup = state.GetComponentLookup<DragSelectableEntity>(true);
        int2 targetToGrid = GridHelper.WorldToGrid(request.targetpos, grid);
        int maxRow = targetToGrid.y + 1;
        int minRow = targetToGrid.y - 1;
        int maxcolumn= targetToGrid.x + 1;
        int mincolumn = targetToGrid.x - 1;
        for (int i=minRow; i<=maxRow; i++)
        {
            for(int j=mincolumn;j<=maxcolumn;j++)
            {
                int cellindex = GridHelper.GetNodeIndex(new int2(j, i), grid);
                if (bucket.TryGetFirstValue(cellindex, out Entity unit, out var it))
                {
                    do
                    {
                        if ((!dragSelectableLookup.HasComponent(unit))&&(!singleselectableLookup.HasComponent(unit)))
                            continue;

                        if (!selectableLookup.HasComponent(unit))
                            continue;
                        float3 pos = transformLookup[unit].Position;
                        if(math.distancesq(pos,request.targetpos)<=0.5f)
                        {
                            if(!selectedLookup.HasComponent(unit))
                            {
                                if(selectableLookup[unit].playerID!=request.playerId)
                                    continue;
                                ecb.AddComponent(unit,new SelectedTag {playerID=request.playerId });
                                return;
                            }
                        }
                        

                    } while (bucket.TryGetNextValue(out unit, ref it));
                }
            }
        }
    }

    public void OnDestroy(ref SystemState state) { }
}
public struct Trapezoid
{
    public float3 B0; // bottom-left
    public float3 B1; // bottom-right
    public float3 T0; // top-left
    public float3 T1; // top-right

    public float MinZ;
    public float MaxZ;

    public Trapezoid(float3 v1, float3 v2, float3 v3, float3 v4)
    {
        float3 a = v1;
        float3 b = v2;
        float3 c = v3;
        float3 d = v4;

        // sort by Z (bottom -> top)
        SortByZ(ref a, ref b);
        SortByZ(ref a, ref c);
        SortByZ(ref a, ref d);
        SortByZ(ref b, ref c);
        SortByZ(ref b, ref d);
        SortByZ(ref c, ref d);

        // bottom edge
        if (a.x < b.x)
        {
            B0 = a;
            B1 = b;
        }
        else
        {
            B0 = b;
            B1 = a;
        }

        // top edge
        if (c.x < d.x)
        {
            T0 = c;
            T1 = d;
        }
        else
        {
            T0 = d;
            T1 = c;
        }

        MinZ = math.min(B0.z, B1.z);
        MaxZ = math.max(T0.z, T1.z);
    }

    static void SortByZ(ref float3 a, ref float3 b)
    {
        if (a.z > b.z)
        {
            float3 tmp = a;
            a = b;
            b = tmp;
        }
    }

    public float LeftX(float z)
    {
        float denom = T0.z - B0.z;
        if (math.abs(denom) < 0.0001f) return math.min(B0.x, T0.x);
        float t = (z - B0.z) / denom;
        return math.lerp(B0.x, T0.x, t);
    }

    public float RightX(float z)
    {
        float denom = T1.z - B1.z;
        if (math.abs(denom) < 0.0001f) return math.max(B1.x, T1.x);
        float t = (z - B1.z) / denom;
        return math.lerp(B1.x, T1.x, t);
    }

    public bool ContainsZ(float z)
    {
        return z >= MinZ && z <= MaxZ;
    }
}