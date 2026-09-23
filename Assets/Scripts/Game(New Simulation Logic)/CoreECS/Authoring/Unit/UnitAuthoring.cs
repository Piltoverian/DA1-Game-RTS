using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

[DisallowMultipleComponent]
public class UnitAuthoring : MonoBehaviour
{
    public UnitSO unitDef;
    public int playerID;
    [Tooltip("-1 starts at MaxHP from BasedSO; otherwise use this instance's initial HP.")]
    public float initialHealth = -1f;

    public class Baker : EntityDefinitionBaker<UnitAuthoring>
    {
        public override void Bake(UnitAuthoring authoring)
        {
            if (GetComponent<BuildingAuthoring>() != null)
                throw new InvalidOperationException("UnitAuthoring and BuildingAuthoring cannot share an entity.");
            DependsOn(authoring.unitDef);
            if (authoring.unitDef == null)
                throw new InvalidOperationException("UnitAuthoring requires a UnitSO.");

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            BakeBase(entity, authoring.unitDef.basedSO, authoring.initialHealth);
            SetComponent(entity, new EntityOwner { PlayerID = authoring.playerID });
            AddComponent(entity, new UnitComponent
            {
                DefinitionID = new FixedString64Bytes(authoring.unitDef.basedSO.ID),
                PopulationCost = authoring.unitDef.PopulationCost
            });
            var costs = AddBuffer<EntityResourceCost>(entity);
            foreach (var cost in authoring.unitDef.ResourceCosts)
                costs.Add(new EntityResourceCost { Type = cost.Type, Amount = cost.Amount });
            var types = AddBuffer<UnitTypeElement>(entity);
            foreach (var type in authoring.unitDef.UnitTypes) types.Add(new UnitTypeElement { Value = type });
        }
    }
}
