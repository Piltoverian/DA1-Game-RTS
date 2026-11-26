using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>Yaw the defense model without rotating its grid collider or navigation footprint.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(BuildingAuthoring))]
public sealed class StationaryDefenseAuthoring : MonoBehaviour
{
    private class Baker : Baker<StationaryDefenseAuthoring>
    {
        public override void Bake(StationaryDefenseAuthoring authoring)
        {
            AddComponent(GetEntity(TransformUsageFlags.Dynamic), new StationaryDefenseAim { Rotation = quaternion.identity });
        }
    }
}

public struct StationaryDefenseAim : IComponentData
{
    public quaternion Rotation;
    public float4x4 RestModel;
    public bool Initialized;
}
