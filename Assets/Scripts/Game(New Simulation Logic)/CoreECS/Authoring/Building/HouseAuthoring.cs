using Unity.Entities;
using UnityEngine;

/// <summary>
/// Attach this authoring component to the SAME ROOT prefab that also has BuildingAuthoring.
/// The population bonus is applied by HousePopulationSystem after construction completes.
/// </summary>
public class HouseAuthoring : MonoBehaviour
{
    [Min(0)]
    public int MaxPopulationIncrease = 5;

    public class Baker : Baker<HouseAuthoring>
    {
        public override void Bake(HouseAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new HouseData
            {
                MaxPopulationIncrease = Mathf.Max(0, authoring.MaxPopulationIncrease)
            });

            // This component remains temporarily after DestroyEntity so the cleanup
            // system can subtract the granted population cap exactly once.
            AddComponent(entity, new HousePopulationCleanup());
        }
    }
}

public struct HouseData : IComponentData
{
    public int MaxPopulationIncrease;
}

/// <summary>
/// Prevents the same house from granting its bonus more than once.
/// </summary>
public struct HousePopulationAppliedTag : IComponentData
{
}

/// <summary>
/// Persists temporarily after entity destruction because this is a cleanup component.
/// </summary>
public struct HousePopulationCleanup : ICleanupComponentData
{
    public int PlayerId;
    public int Amount;
    public byte WasApplied;
}
