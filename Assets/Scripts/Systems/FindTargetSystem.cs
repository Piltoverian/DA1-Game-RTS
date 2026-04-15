using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Physics;
using UnityEngine;
using System.Threading;
partial struct FindTargetSystem : ISystem
{
    // Khai báo Lookup để kiểm tra Component an toàn và nhanh hơn
    private ComponentLookup<Unit> unitLookup;

    public void OnCreate(ref SystemState state)
    {
        unitLookup = state.GetComponentLookup<Unit>(true); // true = ReadOnly
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        PhysicsWorldSingleton physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        CollisionWorld collisionWorld = physicsWorld.CollisionWorld;
        NativeList<DistanceHit> distancesHitList = new NativeList<DistanceHit>(Allocator.Temp);

        // Cập nhật dữ liệu lookup mới nhất cho frame này
        unitLookup.Update(ref state);

        foreach ((
            RefRO<LocalTransform> localTransform,
            RefRW<FindTarget> findTarget,
            RefRW<Target> target)
            in SystemAPI.Query<
                RefRO<LocalTransform>,
                RefRW<FindTarget>,
                RefRW<Target>>())
        {
            findTarget.ValueRW.timer -= SystemAPI.Time.DeltaTime;
            if (findTarget.ValueRO.timer > 0.0f) continue;

            findTarget.ValueRW.timer = findTarget.ValueRO.timerMax;
            distancesHitList.Clear();

            CollisionFilter collisionFilter = new CollisionFilter
            {
                BelongsTo = ~0u,
                CollidesWith = 1u << GameAssets.UNITS_LAYER,
                GroupIndex = 0
            };

            if (collisionWorld.OverlapSphere(localTransform.ValueRO.Position, findTarget.ValueRO.range, ref distancesHitList, collisionFilter))
            {
                foreach (DistanceHit distanceHit in distancesHitList)
                {
                    // KIỂM TRA AN TOÀN TẠI ĐÂY
                    if (unitLookup.HasComponent(distanceHit.Entity))
                    {
                        Unit targetUnit = unitLookup[distanceHit.Entity];
                        if (targetUnit.faction == findTarget.ValueRO.targetFaction)
                        {
                            target.ValueRW.targetEntity = distanceHit.Entity;
                            break;
                        }
                    }
                }
            }
        }
    }
}