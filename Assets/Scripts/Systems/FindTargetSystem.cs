using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Physics;
using UnityEngine;
using System.Threading;
partial struct FindTargetSystem : ISystem
{

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        PhysicsWorldSingleton physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        CollisionWorld collisionWorld = physicsWorld.CollisionWorld;
        NativeList<DistanceHit> distancesHitList = new NativeList<DistanceHit>(Allocator.Temp);

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
            if (findTarget.ValueRO.timer > 0.0f)
            {
                continue;
            }
            findTarget.ValueRW.timer = findTarget.ValueRO.timerMax;
            //Debug.Log("Finding target");
            distancesHitList.Clear();
            CollisionFilter collisionFilter = new CollisionFilter
            {
                BelongsTo = ~0u,
                CollidesWith = 1u << GameAssets.UNITS_LAYER,
                GroupIndex = 0
            };
            //Debug.Log("Checking target");
            if (collisionWorld.OverlapSphere(localTransform.ValueRO.Position, findTarget.ValueRO.range, ref distancesHitList, collisionFilter))
            {
                foreach (DistanceHit distanceHit in distancesHitList)
                {
                    Unit targetUnit = SystemAPI.GetComponent<Unit>(distanceHit.Entity);
                    if (targetUnit.faction == findTarget.ValueRO.targetFaction)
                    {
                        //Debug.Log("found");
                        target.ValueRW.targetEntity = distanceHit.Entity;
                        break;
                    }
                }   
            }
        }
    }
}
