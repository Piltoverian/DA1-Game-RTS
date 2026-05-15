using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;



public class ShootVictimAuthoring : MonoBehaviour
{
    public Transform hitLocation;

    public class Baker : Baker<ShootVictimAuthoring>
    {
        public override void Bake(ShootVictimAuthoring authoring)
        {
            float3 offset = (float3)authoring.hitLocation.localPosition;
            AddComponent(new ShootVictim { localHitOffset = offset });
        }
    }
}
public struct ShootVictim : IComponentData
{
    public float3 localHitOffset;
}
