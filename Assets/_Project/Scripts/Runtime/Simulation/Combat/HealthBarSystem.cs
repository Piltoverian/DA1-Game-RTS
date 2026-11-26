using Unity.Burst;
using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;
using Unity.Transforms;
[UpdateInGroup(typeof(LateSimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
partial struct HealthBarSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        Vector3 cameraForwar = Vector3.zero;

        if (Camera.main != null)
        {
            cameraForwar = Camera.main.transform.forward;
        }
        foreach (var (healthBar, transform) in SystemAPI.Query<RefRW<HealthBar>, RefRW<LocalTransform>>())
        {
            LocalTransform parentLocalTransform = SystemAPI.GetComponent<LocalTransform>(healthBar.ValueRO.healthEntity);
            if (transform.ValueRO.Scale == 1f)
            {
                transform.ValueRW.Rotation = parentLocalTransform.InverseTransformRotation(quaternion.LookRotation(cameraForwar, math.up()));
            }

            EntityHealth health = SystemAPI.GetComponent<EntityHealth>(healthBar.ValueRO.healthEntity);
            if (healthBar.ValueRO.Initialized
                && health.CurrentHP == healthBar.ValueRO.LastDisplayedHP
                && health.MaxHP == healthBar.ValueRO.LastDisplayedMaxHP)
            {
                continue;
            }
            float healthNormalized = health.MaxHP > 0f
                ? math.saturate(health.CurrentHP / health.MaxHP) : 0f;

            if (healthNormalized == 1f)
            {
                transform.ValueRW.Scale = 0f;
            }
            else
            {
                transform.ValueRW.Scale = 1f;
            }
            
            RefRW<PostTransformMatrix> barVisualPostTransformMatrix = SystemAPI.GetComponentRW<PostTransformMatrix>(healthBar.ValueRO.barVisualEntity);
            barVisualPostTransformMatrix.ValueRW.Value = float4x4.Scale(new float3(healthNormalized, 1f, 1f));
            healthBar.ValueRW.LastDisplayedHP = health.CurrentHP;
            healthBar.ValueRW.LastDisplayedMaxHP = health.MaxHP;
            healthBar.ValueRW.Initialized = true;
        }
    }
}
