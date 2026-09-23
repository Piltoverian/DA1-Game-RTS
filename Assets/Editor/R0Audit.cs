using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Read-only asset audit. Reports resolved Unity values without saving scenes or assets.
public static class R0Audit
{
    const string OutputRoot = "Docs/DataRefactor/Evidence/R0/Runs";
    static readonly HashSet<string> Types = new HashSet<string> {
        "BuildingAuthoring", "HealthAuthoring", "UnitAuthoring", "ProductionAuthoring",
        "CommandListAuthoring", "BuildingPrefabCatalogAuthoring", "WorkerAuthoring",
        "BuilderAuthoring", "ShootAttackAuthoring", "TowerAttackAuthoring", "SingleSelectable",
        "DragSelectableObject", "PlayerContextAuthoring", "EnemySpawnerAuthoring", "BuildingPlacer"
    };

    [MenuItem("Tools/R0 Audit/Write effective snapshot %&r")]
    public static void Snapshot()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before taking the authoring snapshot.");
        string output = Path.Combine(OutputRoot, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff"));
        Directory.CreateDirectory(output);
        var report = new StringBuilder();
        report.AppendLine("Unity " + Application.unityVersion + " UTC " + DateTime.UtcNow.ToString("O"));
        report.AppendLine("Missing placement script GUID resolves to: '" + AssetDatabase.GUIDToAssetPath("55a35eb164f1b044a9f091cc9076f5e1") + "'");
        var dependencies = AssetDatabase.GetDependencies("Assets/Scenes/Main.unity", true)
            .Concat(AssetDatabase.GetDependencies("Assets/Scenes/Main/EntitySubscene.unity", true))
            .Distinct().OrderBy(p => p).ToArray();
        File.WriteAllLines(Path.Combine(output, "main-dependencies.txt"), dependencies);
        foreach (var path in Directory.GetFiles("Assets/Scripts/SO", "*.asset").OrderBy(p => p))
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path.Replace('\\', '/'));
            report.AppendLine("ASSET " + path + " loadedType=" + (asset == null ? "NULL" : asset.GetType().FullName));
            if (asset != null) report.AppendLine(EditorJsonUtility.ToJson(asset, true));
        }
        foreach (var path in dependencies.Where(p => p.EndsWith(".prefab")))
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { Dump(root, path, report); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (var path in new[] { "Assets/Scenes/Main.unity", "Assets/Scenes/Main/EntitySubscene.unity" })
        {
            var scene = SceneManager.GetSceneByPath(path);
            if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            report.AppendLine("SCENE " + path + " dirty=" + scene.isDirty + " isSubScene=" + scene.isSubScene);
            foreach (var root in scene.GetRootGameObjects()) Dump(root, path, report);
        }
        File.WriteAllText(Path.Combine(output, "unity-effective-values.txt"), report.ToString());
        Debug.Log("R0_SNAPSHOT_COMPLETE " + output);
    }

    static void Dump(GameObject root, string assetPath, StringBuilder report)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            if (missing > 0) report.AppendLine("MISSING_SCRIPTS " + assetPath + " " + Hierarchy(t) + " count=" + missing);
            foreach (var c in t.GetComponents<Component>())
            {
                if (c == null || !Types.Contains(c.GetType().Name)) continue;
                report.AppendLine("COMPONENT " + assetPath + " :: " + Hierarchy(t) + " :: " + c.GetType().Name
                    + " active=" + t.gameObject.activeInHierarchy + " source=" + PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(c));
                report.AppendLine(EditorJsonUtility.ToJson(c, true));
                report.AppendLine("TRANSFORM position=" + t.position + " localScale=" + t.localScale);
                var so = new SerializedObject(c);
                var property = so.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType == SerializedPropertyType.Integer || property.propertyType == SerializedPropertyType.Enum)
                        report.AppendLine("VALUE " + property.propertyPath + " = " + property.intValue);
                    if (property.propertyType == SerializedPropertyType.Float)
                        report.AppendLine("VALUE " + property.propertyPath + " = " + property.floatValue);
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var value = property.objectReferenceValue;
                    if (value == null) { report.AppendLine("NULL_REF " + property.propertyPath); continue; }
                    report.AppendLine("REF " + property.propertyPath + " = " + AssetDatabase.GetAssetPath(value) + " :: " + value.name);
                    if (value is Transform marker) report.AppendLine("MARKER " + property.propertyPath + " hierarchy=" + Hierarchy(marker) + " ownerLocal=" + t.InverseTransformPoint(marker.position));
                }
            }
        }
    }

    static string Hierarchy(Transform t) => t.parent == null ? t.name : Hierarchy(t.parent) + "/" + t.name;
}
