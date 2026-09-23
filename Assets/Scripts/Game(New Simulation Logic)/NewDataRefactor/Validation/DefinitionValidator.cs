using System;
using System.Collections.Generic;
using UnityEngine;

namespace RTS.DataValidation
{
    internal static class DefinitionValidator
    {
        internal static void Validate(ScriptableObject asset, ValidationContext c)
        {
            switch (asset)
            {
                case BasedSO b:
                    c.Id(b, b.ID, nameof(b.ID), typeof(BasedSO));
                    c.Required(b.Prefab, b, nameof(b.Prefab));
                    c.Number(b.MaxHP, 0, b, nameof(b.MaxHP), true);
                    c.Number(b.WorkRate, 0, b, nameof(b.WorkRate), true);
                    c.References(b.Abilities, b, nameof(b.Abilities), c.IsRegisteredAbility,
                        "BASE_ABILITY_NOT_IN_REGISTRY", "GameDataRegistry.Abilities");
                    c.References(b.Jobs, b, nameof(b.Jobs), c.IsRegisteredJob,
                        "BASE_JOB_NOT_IN_REGISTRY", "GameDataRegistry.Jobs");
                    if (b.Jobs != null && b.Jobs.Count > 0) c.Number(b.JobCapacity, 1, b, nameof(b.JobCapacity));
                    var types = new HashSet<Type>();
                    if (b.Abilities != null)
                        for (int i = 0; i < b.Abilities.Count; i++)
                            if (b.Abilities[i] != null && !types.Add(b.Abilities[i].GetType()))
                                c.Error(b, $"Abilities[{i}]", "ABILITY_SUBTYPE_DUPLICATE", "Only one ability of each subtype is allowed on a Base.");
                    break;
                case UnitSO u:
                    c.Reference(u.basedSO, u, nameof(u.basedSO));
                    c.Number(u.PopulationCost, 1, u, nameof(u.PopulationCost));
                    Costs(u.ResourceCosts, u, nameof(u.ResourceCosts), c);
                    Enums(u.UnitTypes, u, nameof(u.UnitTypes), c);
                    break;
                case BuildingSO b:
                    c.Reference(b.basedSO, b, nameof(b.basedSO));
                    c.Number(b.PopulationCapacity, 0, b, nameof(b.PopulationCapacity));
                    c.Number(b.WorkLoad, 0, b, nameof(b.WorkLoad), true);
                    Costs(b.Cost, b, nameof(b.Cost), c);
                    Enums(b.Tags, b, nameof(b.Tags), c);
                    break;
                case Job job:
                    c.Id(job, job.Id, nameof(job.Id), job.GetType());
                    c.Number(job.WorkLoad, 0, job, nameof(job.WorkLoad), true);
                    switch (job)
                    {
                        case Train train:
                            c.RegisteredReference(train.OutputUnit, train, nameof(train.OutputUnit),
                                c.IsRegisteredUnit, "TRAIN_UNIT_NOT_IN_REGISTRY", "GameDataRegistry.Units");
                            break;
                        case Research research:
                            c.RegisteredReference(research.TechDefinition, research, "techDefinition",
                                c.IsRegisteredTech, "RESEARCH_TECH_NOT_IN_CATALOG",
                                "Nodes of a TechTree referenced by GameDataRegistry.Civs");
                            break;
                        default: c.Error(job, "", "SUBTYPE_UNSUPPORTED", "Unsupported job subtype."); break;
                    }
                    break;
                case TechDefinition tech:
                    c.Id(tech, tech.IDs.ToString(), nameof(tech.IDs), typeof(TechDefinition));
                    Costs(tech.Cost, tech, nameof(tech.Cost), c);
                    break;
                case AbilityDefinition ability:
                    c.Id(ability, ability.Id, nameof(ability.Id), ability.GetType());
                    Ability(ability, c);
                    break;
                default: c.Error(asset, "", "DEFINITION_UNSUPPORTED", "Unsupported definition type."); break;
            }
        }

        private static void Ability(AbilityDefinition ability, ValidationContext c)
        {
            switch (ability)
            {
                case BuildAbilityDefinition b:
                    c.Number(b.Range, 0, b, nameof(b.Range));
                    c.References(b.Buildings, b, nameof(b.Buildings), c.IsRegisteredBuilding,
                        "BUILD_BUILDING_NOT_IN_REGISTRY", "GameDataRegistry.Buildings");
                    break;
                case GatherAbilityDefinition g:
                    c.Number(g.Capacity, 1, g, nameof(g.Capacity));
                    c.Number(g.GatherTime, 0, g, nameof(g.GatherTime), true);
                    c.Number(g.StopDistance, 0, g, nameof(g.StopDistance));
                    c.Number(g.GatherRate, 1, g, nameof(g.GatherRate));
                    break;
                case StorageAbilityDefinition s:
                    Enums(s.AcceptedResourceTypes, s, nameof(s.AcceptedResourceTypes), c);
                    break;
                case AttackAbilityDefinition a:
                    c.Number(a.Range, 0, a, nameof(a.Range));
                    c.Number(a.RotationSpeed, 0, a, nameof(a.RotationSpeed));
                    if (!c.List(a.Weapons, a, nameof(a.Weapons))) break;
                    var slots = new HashSet<int>();
                    for (int i = 0; i < a.Weapons.Count; i++)
                    {
                        var w = a.Weapons[i];
                        var path = $"Weapons[{i}]";
                        c.Required(w.ProjectilePrefab, a, path + ".ProjectilePrefab");
                        c.Number(w.Damage, 0, a, path + ".Damage");
                        c.Number(w.Cooldown, 0, a, path + ".Cooldown", true);
                        c.Number(w.ProjectileSpeed, 0, a, path + ".ProjectileSpeed", true);
                    }
                    break;
                default: c.Error(ability, "", "SUBTYPE_UNSUPPORTED", "Unsupported ability subtype."); break;
            }
        }

        private static void Costs(List<ResourcePair> values, ScriptableObject asset, string field, ValidationContext c)
        {
            if (!c.List(values, asset, field)) return;
            var totals = new Dictionary<ResourceType, double>();
            for (int i = 0; i < values.Count; i++)
            {
                var cost = values[i];
                var path = $"{field}[{i}]";
                if (!Enum.IsDefined(typeof(ResourceType), cost.Type))
                    c.Error(asset, path + ".Type", "ENUM_INVALID", "Unknown resource type.");
                c.Number(cost.Amount, 0, asset, path + ".Amount");
                if (!ValidationContext.Finite(cost.Amount) || cost.Amount < 0) continue;
                if (totals.TryGetValue(cost.Type, out double total))
                    c.Warning(asset, path + ".Type", "COST_RESOURCE_DUPLICATE", "Repeated resource type; baking/payment must sum these entries before checking affordability.");
                double sum = total + cost.Amount;
                if (sum > float.MaxValue)
                    c.Error(asset, path + ".Amount", "COST_TOTAL_OVERFLOW", "Total cost for this resource exceeds the finite float range.");
                totals[cost.Type] = sum;
            }
        }

        private static void Enums<T>(List<T> values, ScriptableObject asset, string field, ValidationContext c) where T : struct, Enum
        {
            if (!c.List(values, asset, field)) return;
            var seen = new HashSet<T>();
            for (int i = 0; i < values.Count; i++)
            {
                if (!Enum.IsDefined(typeof(T), values[i])) c.Error(asset, $"{field}[{i}]", "ENUM_INVALID", "Unknown enum value.");
                if (!seen.Add(values[i])) c.Warning(asset, $"{field}[{i}]", "ENUM_DUPLICATE", "Repeated enum value.");
            }
        }
    }
}
