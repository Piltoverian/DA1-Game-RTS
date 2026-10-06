using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[DisallowMultipleComponent]
public class BuildingAuthoring : MonoBehaviour
{
    public BuildingSO buildingDef;
    [Tooltip("-1 starts at MaxHP from BasedSO; otherwise use this instance's initial HP.")]
    public float initialHealth = -1f;
    [Range(0f, 1f)] public float initialConstructionProgress;

    public class Baker : EntityDefinitionBaker<BuildingAuthoring>
    {
        public override void Bake(BuildingAuthoring authoring)
        {
            if (GetComponent<UnitAuthoring>() != null)
                throw new InvalidOperationException("BuildingAuthoring and UnitAuthoring cannot share an entity.");
            DependsOn(authoring.buildingDef);
            if (authoring.buildingDef == null)
                throw new InvalidOperationException("BuildingAuthoring requires a BuildingSO.");
            if (!math.isfinite(authoring.buildingDef.WorkLoad) || authoring.buildingDef.WorkLoad <= 0f)
                throw new InvalidOperationException("BuildingSO.WorkLoad must be finite and positive.");
            float progress = authoring.initialConstructionProgress;
            if (!math.isfinite(progress) || progress < 0f || progress > 1f)
                throw new InvalidOperationException("Initial construction progress must be between 0 and 1.");

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            BakeBase(entity, authoring.buildingDef.basedSO, authoring.initialHealth);
            AddComponent(entity, new BuildingComponent
            {
                DefinitionID = new FixedString64Bytes(authoring.buildingDef.basedSO.ID),
                WorkLoad = authoring.buildingDef.WorkLoad,
                PopulationCapacity = authoring.buildingDef.PopulationCapacity
            });
            var costs = AddBuffer<EntityResourceCost>(entity);
            foreach (var cost in authoring.buildingDef.Cost)
                costs.Add(new EntityResourceCost { Type = cost.Type, Amount = cost.Amount });
            var tags = AddBuffer<BuildingTagElement>(entity);
            foreach (var tag in authoring.buildingDef.Tags) tags.Add(new BuildingTagElement { Value = tag });
            AddComponent(entity, new RevealHeightProperty { Value = math.lerp(-10f, 10f, progress) });
            AddComponent(entity, new BuildingConstruction
            {
                CompletedWork = progress * authoring.buildingDef.WorkLoad,
                Phase = progress >= 1f ? ConstructionPhase.Completed :
                    progress > 0f ? ConstructionPhase.UnderConstruction : ConstructionPhase.Planned
            });
        }
    }
}
