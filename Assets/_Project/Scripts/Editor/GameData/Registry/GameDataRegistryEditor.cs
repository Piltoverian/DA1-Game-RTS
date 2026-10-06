using System.Collections.Generic;
using RTS.DataValidation;
using RTS.DataEditor;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameDataRegistry))]
public sealed class GameDataRegistryEditor : Editor
{
    private IReadOnlyList<ValidationIssue> issues;

    public override void OnInspectorGUI()
    {
        var registry = (GameDataRegistry)target;
        var before = RegistryDefinitionIds.Collect(registry);
        if (DrawDefaultInspector())
        {
            RegistryDefinitionIds.AssignMissing(registry, before);
            issues = null;
        }
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Base IDs are read-only and assigned only with Assign Missing IDs. Other new registry references receive missing IDs automatically. Existing IDs are preserved. For references added inside a Base or TechTree, use Assign Missing IDs. Save assets after editing.", MessageType.Info);
        if (GUILayout.Button("Assign Missing IDs"))
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Assign Registry Definition IDs");
            int assigned = RegistryDefinitionIds.AssignMissing(registry);
            Undo.CollapseUndoOperations(group);
            issues = null;
            Debug.Log($"Assigned {assigned} missing definition ID(s) in '{registry.name}'.", registry);
        }
        if (GUILayout.Button("Validate"))
        {
            issues = GameDataValidator.Validate((GameDataRegistry)target);
            LogIssues(issues, target);
        }
        if (issues == null) return;
        Count(issues, out int errors, out int warnings);
        EditorGUILayout.HelpBox($"Last validation: {errors} error(s), {warnings} warning(s). Run Validate again after editing any referenced asset.",
            errors > 0 ? MessageType.Error : warnings > 0 ? MessageType.Warning : MessageType.Info);
        foreach (var issue in issues)
        {
            EditorGUILayout.HelpBox(Format(issue), issue.Severity == ValidationSeverity.Error ? MessageType.Error : MessageType.Warning);
            if (issue.Asset != null && GUILayout.Button("Locate asset: " + issue.Asset.name))
                EditorGUIUtility.PingObject(issue.Asset);
            if (issue.RelatedAsset != null && GUILayout.Button("Locate related asset: " + issue.RelatedAsset.name))
                EditorGUIUtility.PingObject(issue.RelatedAsset);
        }
    }

    [MenuItem("Tools/Game Data/Validate Selected Registry")]
    private static void ValidateSelected()
    {
        var registry = Selection.activeObject as GameDataRegistry;
        LogIssues(GameDataValidator.Validate(registry), registry);
    }

    [MenuItem("Tools/Game Data/Validate Selected Registry", true)]
    private static bool CanValidateSelected() => Selection.activeObject is GameDataRegistry;

    private static string Format(ValidationIssue issue)
    {
        string path = issue.Asset != null ? AssetDatabase.GetAssetPath(issue.Asset) : "<missing registry>";
        string related = issue.RelatedAsset != null ? "\nRelated: " + AssetDatabase.GetAssetPath(issue.RelatedAsset) : "";
        return $"[{issue.Code}] {path}\n{issue.FieldPath}: {issue.Message}{related}";
    }

    private static void LogIssues(IReadOnlyList<ValidationIssue> results, Object registry)
    {
        foreach (var issue in results)
        {
            if (issue.Severity == ValidationSeverity.Error) Debug.LogError(Format(issue), issue.Asset);
            else Debug.LogWarning(Format(issue), issue.Asset);
        }
        Count(results, out int errors, out int warnings);
        Debug.Log($"Game Data validation: {errors} error(s), {warnings} warning(s).", registry);
    }

    private static void Count(IReadOnlyList<ValidationIssue> results, out int errors, out int warnings)
    {
        errors = warnings = 0;
        foreach (var issue in results)
            if (issue.Severity == ValidationSeverity.Error) errors++; else warnings++;
    }
}
