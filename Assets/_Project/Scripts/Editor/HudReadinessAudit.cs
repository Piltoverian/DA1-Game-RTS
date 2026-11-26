using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Read-only content audit. No asset migration, balance edits, or gameplay spawn.</summary>
public static class HudReadinessAudit
{
    [MenuItem("Tools/RTS UI/Export content readiness report")]
    public static void Export()
    {
        var report = new StringBuilder("# UI content readiness\n\nGenerated from saved assets. This is not a gameplay test.\n\n");
        var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/_Project/Resources/GameReg.asset");
        report.AppendLine("## Registered buildings");
        if (registry == null) report.AppendLine("ERROR: GameReg unavailable.");
        else foreach (var definition in registry.Buildings)
        {
            if (definition == null) { report.AppendLine("ERROR: Missing building reference."); continue; }
            var data = definition.basedSO;
            report.Append("- ").Append(definition.name).Append(": ");
            if (data == null) { report.AppendLine("ERROR: BasedSO not assigned."); continue; }
            report.Append(data.Name).Append(" / ").Append(data.ID);
            if (data.Icon == null) report.Append("; missing icon");
            if (data.Prefab == null) report.Append("; missing prefab");
            report.AppendLine();
        }
        report.AppendLine("\n## Legacy building candidates");
        foreach (string name in new[] { "TowerDefend", "TowerDefend2", "TowerDefendEnemy", "PlasmaTower", "ResourceDepot", "Barracks" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/" + name + ".prefab");
            report.Append("- ").Append(name).Append(": ");
            if (prefab == null) { report.AppendLine("not found"); continue; }
            var building = prefab.GetComponent<BuildingAuthoring>();
            if (building == null || building.buildingDef == null) report.Append("building definition not bound; ");
            if (building != null && prefab.GetComponent<UnitAuthoring>() != null) report.Append("conflicting BuildingAuthoring/UnitAuthoring; ");
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab) > 0) report.Append("missing root scripts; ");
            report.AppendLine("requires bake/runtime verification before demonstration");
        }
        Directory.CreateDirectory("Temp");
        File.WriteAllText("Temp/HudContentReadiness.md", report.ToString());
        Debug.Log("UI content report saved: Temp/HudContentReadiness.md (no assets modified).");
    }
}
