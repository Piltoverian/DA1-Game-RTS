using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

partial struct MiniMapUISystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        
    }

    public void OnUpdate(ref SystemState state)
    {
        foreach (var (minimapRenderer, entity) in SystemAPI.Query<MinimapRenderer>().WithEntityAccess())
        {
            var parent= SystemAPI.GetComponent<Parent>(entity);
            var selected = SystemAPI.GetComponent<Selected>(parent.Value);
            var selectedRenderer = state.EntityManager.GetComponentObject<SpriteRenderer>(minimapRenderer.selectedIcon);
            var nonSelectRenderer = state.EntityManager.GetComponentObject<SpriteRenderer>(minimapRenderer.nonSelectIcon);
            if (state.EntityManager.IsComponentEnabled<Selected>(parent.Value))
            {
                selectedRenderer.enabled = true;
                nonSelectRenderer.enabled = false;
            }
            else
            { 
                selectedRenderer.enabled = false;
                nonSelectRenderer.enabled = true;
            }
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
