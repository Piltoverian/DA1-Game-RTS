using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;

[UpdateBefore(typeof(ShootAttackSystem))]
[BurstCompile]
public partial struct FindTargetSystem : ISystem
{
    private ComponentLookup<EntityOwner> unitLookup;
    private ComponentLookup<EntityHealth> healthLookup;

    public void OnCreate(ref SystemState state)
    {
        unitLookup = state.GetComponentLookup<EntityOwner>(true);
        healthLookup = state.GetComponentLookup<EntityHealth>(true);

        state.RequireForUpdate<PhysicsWorldSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        PhysicsWorldSingleton physicsWorld =
            SystemAPI.GetSingleton<PhysicsWorldSingleton>();

        CollisionWorld collisionWorld =
            physicsWorld.CollisionWorld;

        unitLookup.Update(ref state);
        healthLookup.Update(ref state);

        NativeList<DistanceHit> distanceHitList =
            new NativeList<DistanceHit>(Allocator.Temp);

        foreach (var (
                     localTransform,
                     findTarget,
                     target, owner)
                 in SystemAPI.Query<
                     RefRO<LocalTransform>,
                     RefRW<FindTarget>,
                     RefRW<Target>, RefRO<EntityOwner>>())
        {
            Entity current = target.ValueRO.targetEntity;
            if (current != Entity.Null && healthLookup.HasComponent(current) && healthLookup[current].CurrentHP > 0 && unitLookup.HasComponent(current) && unitLookup[current].PlayerID != owner.ValueRO.PlayerID) continue;
            findTarget.ValueRW.timer -= SystemAPI.Time.DeltaTime;

            if (findTarget.ValueRO.timer > 0f)
                continue;

            findTarget.ValueRW.timer = findTarget.ValueRO.timerMax;

            distanceHitList.Clear();

            CollisionFilter collisionFilter = new CollisionFilter
            {
                BelongsTo = PhysicsLayersDefine.Everything,

                // Tìm cả EntityOwner và Building
                CollidesWith =
                    PhysicsLayersDefine.Units |
                    PhysicsLayersDefine.Building,

                GroupIndex = 0
            };

            bool hasHit = collisionWorld.OverlapSphere(
                localTransform.ValueRO.Position,
                findTarget.ValueRO.range,
                ref distanceHitList,
                collisionFilter
            );

            if (!hasHit)
            {
                target.ValueRW.targetEntity = Entity.Null;
                continue;
            }

            Entity bestTarget = Entity.Null;
            float bestDistanceSq = float.MaxValue;

            for (int i = 0; i < distanceHitList.Length; i++)
            {
                Entity hitEntity = distanceHitList[i].Entity;

                if (!healthLookup.HasComponent(hitEntity))
                    continue;

                if (!IsWantedTarget(hitEntity, owner.ValueRO.PlayerID))
                    continue;

                float distanceSq = distanceHitList[i].Distance * distanceHitList[i].Distance;

                if (distanceSq < bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    bestTarget = hitEntity;
                }
            }

            target.ValueRW.targetEntity = bestTarget;
        }

        distanceHitList.Dispose();
    }

    private bool IsWantedTarget(Entity entity, int wantedPlayerID)
    {
        if (unitLookup.HasComponent(entity))
        {
            return unitLookup[entity].PlayerID != wantedPlayerID && healthLookup[entity].CurrentHP > 0;
        }

        return false;
    }
}

