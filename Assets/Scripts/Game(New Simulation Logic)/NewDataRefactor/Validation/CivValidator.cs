using System.Collections.Generic;
using UnityEngine;

namespace RTS.DataValidation
{
    internal static class CivValidator
    {
        internal static void Validate(CivDef civ, ValidationContext c)
        {
            c.Id(civ, civ.Id, nameof(civ.Id), typeof(CivDef));
            c.Reference(civ.TechTree, civ, nameof(civ.TechTree));

            if (!c.List(civ.UnitUnlocks, civ, nameof(civ.UnitUnlocks))) return;

            var validTechs = new HashSet<TechDefinition>();
            if (civ.TechTree != null && civ.TechTree.Nodes != null)
            {
                for (int i = 0; i < civ.TechTree.Nodes.Count; i++)
                {
                    var node = civ.TechTree.Nodes[i];
                    if (node != null && node.techDefinition != null)
                        validTechs.Add(node.techDefinition);
                }
            }

            var localUnits = new HashSet<UnitSO>();
            for (int i = 0; i < civ.UnitUnlocks.Count; i++)
            {
                var entry = civ.UnitUnlocks[i];
                string entryPath = $"{nameof(civ.UnitUnlocks)}[{i}]";
                if (entry == null)
                {
                    c.Error(civ, entryPath, "CIV_UNLOCK_ENTRY_NULL", "Unlock entry must not be null.");
                    continue;
                }

                var unitPath = $"{entryPath}.{nameof(entry.unitDefinition)}";
                if (!c.Required(entry.unitDefinition, civ, unitPath)) continue;

                if (!localUnits.Add(entry.unitDefinition))
                {
                    c.Error(civ, unitPath, "CIV_UNIT_DUPLICATE", $"Unit '{entry.unitDefinition.name}' appears more than once in this Civ's roster.", entry.unitDefinition);
                }

                if (!c.IsRegisteredUnit(entry.unitDefinition))
                {
                    c.Error(civ, unitPath, "CIV_UNIT_NOT_IN_REGISTRY", $"Unit '{entry.unitDefinition.name}' in Civ roster is not registered in GameDataRegistry.Units.", entry.unitDefinition);
                }

                if (entry.unitDefinition.basedSO == null || string.IsNullOrWhiteSpace(entry.unitDefinition.basedSO.ID))
                {
                    c.Error(civ, unitPath, "CIV_UNIT_ID_MISSING", $"Unit '{entry.unitDefinition.name}' does not have a valid Base ID.", entry.unitDefinition);
                }

                var prereqsPath = $"{entryPath}.{nameof(entry.Prerequisites)}";
                if (!c.List(entry.Prerequisites, civ, prereqsPath)) continue;

                var localPrereqs = new HashSet<TechDefinition>();
                for (int j = 0; j < entry.Prerequisites.Count; j++)
                {
                    var tech = entry.Prerequisites[j];
                    string techPath = $"{prereqsPath}[{j}]";
                    if (!c.Required(tech, civ, techPath)) continue;

                    if (!localPrereqs.Add(tech))
                    {
                        c.Error(civ, techPath, "CIV_PREREQUISITE_DUPLICATE", $"Tech prerequisite '{tech.name}' appears more than once for unit '{entry.unitDefinition.name}'.", tech);
                        continue;
                    }

                    if (!validTechs.Contains(tech))
                    {
                        c.Error(civ, techPath, "CIV_PREREQUISITE_OUTSIDE_TREE", $"Tech requirement '{tech.name}' for unit '{entry.unitDefinition.name}' does not belong to Civ's TechTree.", tech);
                    }
                    else c.Visit(tech);
                }
            }
        }
    }
}
