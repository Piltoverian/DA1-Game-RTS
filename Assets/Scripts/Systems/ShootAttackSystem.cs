using Unity.Burst;
using Unity.Entities;
using UnityEngine;

partial struct ShootAttackSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach ((
            RefRW<ShootAttack> shootAttack,
            RefRW<Target> target) 
            in SystemAPI.Query<
                RefRW<ShootAttack>,
                RefRW<Target>>())
        {
            shootAttack.ValueRW.timer -= SystemAPI.Time.DeltaTime;

            if (shootAttack.ValueRW.timer > 0f)
                continue;

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

            shootAttack.ValueRW.timer = shootAttack.ValueRO.timerMax;

            RefRW<Health> targetHealth = SystemAPI.GetComponentRW<Health>(targetEntity);
            int damageAmount = 1;
            targetHealth.ValueRW.healthAmount -= damageAmount;
        }
    }
}