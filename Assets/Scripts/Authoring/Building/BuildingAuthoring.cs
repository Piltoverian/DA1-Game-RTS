using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

public enum BuildingType
{
    Barracks,
    Tower,
    ResourceDepot
}

public class BuildingAuthoring : MonoBehaviour
{
    [Header("Building Info")]
    public BuildingType BuildingType;

    [Header("Construction")]
    public float ConstructionTime = 10f;
    public float StartRevealHeight = 0f;
    public float EndRevealHeight = 15f;

    [Header("Placement Size")]
    public float FootprintSizeX = 6f;
    public float FootprintSizeZ = 6f;
    public float BlockerHeight = 4f;

    class Baker : Baker<BuildingAuthoring>
    {
        public override void Bake(BuildingAuthoring src)
        {
            Entity e = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(e, new BuildingData
            {
                Type = src.BuildingType,
                ConstructionTime = src.ConstructionTime,
                FootprintSizeX = src.FootprintSizeX,
                FootprintSizeZ = src.FootprintSizeZ,
                BlockerHeight = src.BlockerHeight
            });

            AddComponent(e, new ConstructionData
            {
                TotalTime = src.ConstructionTime,
                Elapsed = 0f,
                StartRevealHeight = src.StartRevealHeight,
                EndRevealHeight = src.EndRevealHeight
            });

            AddComponent(e, new RevealHeightProperty
            {
                Value = src.StartRevealHeight
            });

            AddComponent<UnderConstructionTag>(e);
        }
    }
}

public struct BuildingData : IComponentData
{
    public BuildingType Type;
    public float ConstructionTime;

    public float FootprintSizeX;
    public float FootprintSizeZ;
    public float BlockerHeight;
}

public struct ConstructionData : IComponentData
{
    public float TotalTime;
    public float Elapsed;
    public float StartRevealHeight;
    public float EndRevealHeight;
}

[MaterialProperty("_RevealHeight")]
public struct RevealHeightProperty : IComponentData
{
    public float Value;
}

public struct UnderConstructionTag : IComponentData
{
}