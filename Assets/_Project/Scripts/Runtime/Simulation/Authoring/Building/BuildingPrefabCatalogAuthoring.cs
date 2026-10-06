using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public struct BuildingPrefabCatalogTag : IComponentData
{
}

public struct BuildingPrefabCatalogElement : IBufferElementData
{
    public int CommandIndex;
    public Entity Prefab;
}

public class BuildingPrefabCatalogAuthoring : MonoBehaviour
{
    public List<BuildingPrefabEntry> Buildings = new();

    [Serializable]
    public class BuildingPrefabEntry
    {
        public int CommandIndex;
        public GameObject BuildingPrefab;
    }

    public class Baker : Baker<BuildingPrefabCatalogAuthoring>
    {
        public override void Bake(BuildingPrefabCatalogAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);

            AddComponent<BuildingPrefabCatalogTag>(entity);

            DynamicBuffer<BuildingPrefabCatalogElement> buffer =
                AddBuffer<BuildingPrefabCatalogElement>(entity);

            foreach (BuildingPrefabEntry entry in authoring.Buildings)
            {
                if (entry.BuildingPrefab == null)
                    continue;

                buffer.Add(new BuildingPrefabCatalogElement
                {
                    CommandIndex = entry.CommandIndex,
                    Prefab = GetEntity(entry.BuildingPrefab, TransformUsageFlags.Dynamic)
                });
            }
        }
    }
}