using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;

[BurstCompile]
public partial struct FindTargetSystem : ISystem
{
    private ComponentLookup<Unit> unitLookup;
    private ComponentLookup<BuildingData> buildingLookup;
    private ComponentLookup<Health> healthLookup;

    public void OnCreate(ref SystemState state)
    {
        unitLookup = state.GetComponentLookup<Unit>(true);
        buildingLookup = state.GetComponentLookup<BuildingData>(true);
        healthLookup = state.GetComponentLookup<Health>(true);

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
        buildingLookup.Update(ref state);
        healthLookup.Update(ref state);

        NativeList<DistanceHit> distanceHitList =
            new NativeList<DistanceHit>(Allocator.Temp);

        foreach (var (
                     localTransform,
                     findTarget,
                     target)
                 in SystemAPI.Query<
                     RefRO<LocalTransform>,
                     RefRW<FindTarget>,
                     RefRW<Target>>())
        {
            findTarget.ValueRW.timer -= SystemAPI.Time.DeltaTime;

            if (findTarget.ValueRO.timer > 0f)
                continue;

            findTarget.ValueRW.timer = findTarget.ValueRO.timerMax;

            distanceHitList.Clear();

            CollisionFilter collisionFilter = new CollisionFilter
            {
                BelongsTo = PhysicsLayersDefine.Everything,

                // Tìm cả Unit và Building
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

                if (!IsWantedTarget(hitEntity, findTarget.ValueRO.playerID))
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
            Unit unit = unitLookup[entity];

            return unit.playerID == wantedPlayerID;
        }

        if (buildingLookup.HasComponent(entity))
        {
            BuildingData building = buildingLookup[entity];

            return building.PlayerID == wantedPlayerID;
        }

        return false;
    }
}