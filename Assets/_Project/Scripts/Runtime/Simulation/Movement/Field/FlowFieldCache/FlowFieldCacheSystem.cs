using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
partial struct FlowFieldCacheInitSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        Entity cache = state.EntityManager.CreateSingleton<FlowFieldCache>();
        state.EntityManager.AddBuffer<FlowFieldCacheEntry>(cache);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Enabled = false;   
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}

