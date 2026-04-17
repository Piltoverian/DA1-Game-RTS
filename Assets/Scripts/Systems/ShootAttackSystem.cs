using Microsoft.Win32.SafeHandles;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
[UpdateBefore(typeof(HealthDeadTestSystem))]
partial struct ShootAttackSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EntitiesReferences>();
        state.RequireForUpdate<GridComponent>();
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntitiesReferences entitiesReFerences = SystemAPI.GetSingleton<EntitiesReferences>();
        GridComponent gridComponent = SystemAPI.GetSingleton<GridComponent>();

        var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

        foreach ((
            RefRW<LocalTransform> localTransform,
            RefRW<ShootAttack> shootAttack,
            RefRW<Target> target,
            RefRW<MovementAgentComponent> movementAgent,
            Entity entity) 
            in SystemAPI.Query<
                RefRW<LocalTransform>,
                RefRW<ShootAttack>,
                RefRW<Target>,
                RefRW<MovementAgentComponent>>().
                WithEntityAccess())
        {

            Entity targetEntity = target.ValueRO.targetEntity;

            if (targetEntity == Entity.Null || !SystemAPI.Exists(targetEntity))
            {
                target.ValueRW.targetEntity = Entity.Null; 
                continue;
            }

            if (!SystemAPI.HasComponent<Health>(targetEntity))
            {
                target.ValueRW.targetEntity = Entity.Null;
                continue;
            }

            LocalTransform targetLocalTransform = SystemAPI.GetComponent<LocalTransform>(targetEntity);
            if (math.distance(localTransform.ValueRO.Position, targetLocalTransform.Position) > shootAttack.ValueRO.attackDistance)
            {
                MovementAgentAPI.SetTarget(state.EntityManager, entity, targetLocalTransform.Position, gridComponent, ecb);
                continue;
            }
            else
            {
                MovementAgentAPI.StopAgent(state.EntityManager, entity, ecb);
            }

            float3 aimDirection = math.normalize(targetLocalTransform.Position - localTransform.ValueRO.Position);
            if (!aimDirection.Equals(float3.zero))
            {
                quaternion targetRotation = quaternion.LookRotationSafe(aimDirection, math.up());
                localTransform.ValueRW.Rotation = math.slerp(localTransform.ValueRO.Rotation, targetRotation, 8f * SystemAPI.Time.DeltaTime);
            }


            shootAttack.ValueRW.timer -= SystemAPI.Time.DeltaTime;

            if (shootAttack.ValueRW.timer > 0f)
                continue;
            shootAttack.ValueRW.timer = shootAttack.ValueRO.timerMax;
            Entity bulletEntity = state.EntityManager.Instantiate(entitiesReFerences.bulletPrefab);
            SystemAPI.SetComponent(bulletEntity,LocalTransform.FromPosition(localTransform.ValueRO.Position) );

            RefRW<Bullet> bullet = SystemAPI.GetComponentRW<Bullet>(bulletEntity);
            bullet.ValueRW.damage = shootAttack.ValueRO.damage;

            RefRW<Target> bulletTarget = SystemAPI.GetComponentRW<Target>(bulletEntity);
            bulletTarget.ValueRW.targetEntity = target.ValueRO.targetEntity;
        }
    }
}