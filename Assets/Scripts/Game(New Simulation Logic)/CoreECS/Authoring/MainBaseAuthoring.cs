using Unity.Entities;
using UnityEngine;

/// <summary>
/// Attach this authoring component to the main base GameObject.
/// The same GameObject should also contain BuildingAuthoring so the ECS entity
/// has BuildingData.PlayerID.
/// </summary>
public class MainBaseAuthoring : MonoBehaviour
{
    public class Baker : Baker<MainBaseAuthoring>
    {
        public override void Bake(MainBaseAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<MainBaseTag>(entity);
        }
    }
}

/// <summary>
/// Marks a building as a player's main base.
/// </summary>
public struct MainBaseTag : IComponentData
{
}
