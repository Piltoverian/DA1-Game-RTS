using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Hệ thống Visual Debug cho Movement Agent.
/// Chạy trên Main Thread để sử dụng UnityEngine.Debug.DrawLine.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial struct MovementAgentDebugSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<MovementAgentDebugConfig>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<MovementAgentDebugConfig>())
        {
            Debug.LogWarning("Missing MovementAgentDebugConfig singleton! Make sure to add MovementAgentDebugAuthoring to a GameObject.");
            return;
        }

        var config = SystemAPI.GetSingleton<MovementAgentDebugConfig>();
        int agentCount = 0;
        
        foreach (var (transform, agent, avoidance, contextMap, entity) 
            in SystemAPI.Query<RefRO<LocalTransform>, RefRO<MovementAgentComponent>, RefRO<MovementAgentAvoidanceComponent>, DynamicBuffer<ContextMapElement>>()
            .WithEntityAccess())
        {
            agentCount++;
            float3 pos = transform.ValueRO.Position + new float3(0, 1.0f, 0); // Vẽ cao hơn 1m để không bị lấp

            // 1. Vẽ Vận tốc hiện tại (Trắng)
            if (config.ShowVelocity && math.lengthsq(agent.ValueRO.velocity) > 0.01f)
            {
                Debug.DrawRay(pos, agent.ValueRO.velocity, Color.white, 0.05f, false);
            }

            // 2. Vẽ Hướng mong muốn (Xanh lá)
            if (config.ShowDesiredVelocity)
            {
                float3 desiredDir = math.normalizesafe(agent.ValueRO.realTarget - transform.ValueRO.Position);
                Debug.DrawRay(pos + new float3(0, 0.2f, 0), desiredDir * 3f, Color.green, 0.05f, false);
            }

            // 3. Vẽ Context Steering Rays (Tia Né Tránh)
            if (config.ShowContextSteer && contextMap.Length > 0)
            {
                int res = contextMap.Length;
                for (int i = 0; i < res; i++)
                {
                    float angle = i * 2 * math.PI / res;
                    float3 dir = new float3(math.cos(angle), 0, math.sin(angle)); 
                    
                    var element = contextMap[i];
                    
                    // Vẽ tia Interest (Xanh dương) - Cao hơn một chút
                    if (element.Interest > 0.05f)
                    {
                        Debug.DrawRay(pos + new float3(0, 0.4f, 0), dir * element.Interest * 2f, Color.cyan, 0.05f, false);
                    }
                    
                    // Vẽ tia Danger (Đỏ) - Thấp hơn một chút
                    if (element.Danger > 0.05f)
                    {
                        Debug.DrawRay(pos + new float3(0, 0.3f, 0), dir * element.Danger * 2f, Color.red, 0.05f, false);
                    }
                }
            }

            // 4. Vẽ Đường nối và Điểm Mục tiêu
            if (config.ShowTargetLines)
            {
                // Đường tới điểm Slot trong đội hình (Vàng Cam) - Vẽ ngay khi có dữ liệu Slot
                if (math.lengthsq(agent.ValueRO.slotTarget) > 0.001f)
                {
                    Debug.DrawLine(pos, agent.ValueRO.slotTarget, new Color(1f, 0.6f, 0f), 0.05f, false);
                    DrawCross(agent.ValueRO.slotTarget, 0.6f, new Color(1f, 0.6f, 0f));
                }
                
                // Đường tới Target thực tế trên đảo (Tím) - Luôn vẽ
                Debug.DrawLine(pos, agent.ValueRO.realTarget, new Color(0.5f, 0, 1f), 0.05f, false);
                DrawCross(agent.ValueRO.realTarget, 0.8f, new Color(0.5f, 0, 1f));
            }

            // 5. Vẽ Vòng tròn bán kính (Proximity)
            if (config.ShowProximity)
            {
                DrawCircle(pos, avoidance.ValueRO.radius, Color.gray);
            }
        }
        
        if (agentCount > 0 && Time.frameCount % 60 == 0)
        {
            Debug.Log($"MovementAgentDebugSystem: Drawing {agentCount} agents.");
        }
    }

    private void DrawCircle(float3 center, float radius, Color color)
    {
        int segments = 12;
        for (int i = 0; i < segments; i++)
        {
            float a1 = i * 2 * math.PI / segments;
            float a2 = (i + 1) * 2 * math.PI / segments;
            
            float3 p1 = center + new float3(math.sin(a1), 0, math.cos(a1)) * radius;
            float3 p2 = center + new float3(math.sin(a2), 0, math.cos(a2)) * radius;
            
            Debug.DrawLine(p1, p2, color, 0.05f, false);
        }
    }

    private void DrawCross(float3 center, float size, Color color)
    {
        float s = size * 0.5f;
        Debug.DrawLine(center + new float3(-s, 0, 0), center + new float3(s, 0, 0), color, 0.05f, false);
        Debug.DrawLine(center + new float3(0, 0, -s), center + new float3(0, 0, s), color, 0.05f, false);
        Debug.DrawLine(center + new float3(0, -s, 0), center + new float3(0, s, 0), color, 0.05f, false);
    }
}
