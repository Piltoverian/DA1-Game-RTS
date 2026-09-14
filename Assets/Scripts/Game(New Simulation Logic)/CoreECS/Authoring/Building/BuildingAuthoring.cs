using JetBrains.Annotations;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

public enum BuildingType
{
    Barracks,
    Tower,
    ResourceDepot,
    House
}

public enum BuildingState
{
    StartBuild,
    UnderConstruction,
    Completed,
    Destroyed
}

public class BuildingAuthoring : MonoBehaviour
{
    [Header("Data")]
    public BuildingDefinition buildingDef;

    class Baker : Baker<BuildingAuthoring>
    {
        public override void Bake(BuildingAuthoring src)
        {
            Entity e = GetEntity(TransformUsageFlags.Dynamic);

            BuildingType type = src.buildingDef != null ? src.buildingDef.BuildingType : BuildingType.Barracks;
            float totalWork = src.buildingDef != null ? src.buildingDef.TotalWorkLoad : 100f;

            AddComponent(e, new BuildingData
            {
                Type = type,
                TotalWorkLoad = totalWork
            });

            AddComponent(e, new ConstructionData
            {
                currentWorkLoad = 0f
            });

            var costBuffer = AddBuffer<BuildingCost>(e);
            if (src.buildingDef != null && src.buildingDef.Cost != null)
            {
                foreach (var c in src.buildingDef.Cost)
                {
                    if (c.Amount <= 0f) continue;
                    bool isFound = false;
                    for (int i = 0; i < costBuffer.Length; i++)
                    {
                        if (costBuffer[i].Type == c.Type)
                        {
                            var existing = costBuffer[i];
                            existing.Amount += c.Amount;
                            costBuffer[i] = existing;
                            isFound = true;
                            break;
                        }
                    }

                    if (!isFound)
                    {
                        costBuffer.Add(new BuildingCost
                        {
                            Type = c.Type,
                            Amount = c.Amount
                        });
                    }
                }
            }

            if (type == BuildingType.ResourceDepot)
            {
                AddComponent<ResourceDepotTag>(e);
            }

            AddComponent(e, new BuildingStateComponent
            {
                Current = BuildingState.StartBuild,
                Previous = BuildingState.StartBuild
            });
            AddComponent(e, new RevealHeightProperty { Value = -10f });
        }
    }
}

public struct BuildingStateComponent : IComponentData
{
    public BuildingState Current;
    public BuildingState Previous;
}

public struct BuildingData : IComponentData
{
    public BuildingType Type;
    public float TotalWorkLoad;
}

public struct ConstructionData : IComponentData
{
    public float currentWorkLoad;
}

public struct ResourceDepotTag : IComponentData
{
}

public struct BuildingCost: IBufferElementData
{
    public ResourceType Type;
    public float Amount;
}