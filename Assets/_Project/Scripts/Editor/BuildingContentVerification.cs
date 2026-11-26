using System;
using System.IO;
using System.Linq;
using System.Text;
using RTS.DataValidation;
using UnityEditor;
using UnityEngine;

/// <summary>Saved-content integration checks. Does not simulate combat, construction or gathering.</summary>
public static class BuildingContentVerification
{
    [MenuItem("Tools/RTS UI/Verify integrated buildings")]
    public static void VerifyMenu() => Verify();

    public static void RunBatch()
    {
        try { EditorApplication.Exit(Verify() ? 0 : 1); }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    public static bool Verify()
    {
        int errors = 0;
        var text = new StringBuilder("# Building integration verification\n\nSaved Unity assets only; not a bake or gameplay test.\n\n");
        void Check(bool valid, string message)
        {
            text.Append(valid ? "PASS: " : "FAIL: ").AppendLine(message);
            if (!valid) errors++;
        }
        var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/_Project/Resources/GameReg.asset");
        Check(registry != null, "GameReg loads");
        if (registry == null) return false;
        foreach (var issue in GameDataValidator.Validate(registry))
        {
            text.Append(issue.Severity).Append(": ").Append(issue.Code).Append(' ').Append(issue.Message).AppendLine();
            if (issue.Severity == ValidationSeverity.Error) errors++;
        }
        var build = AssetDatabase.LoadAssetAtPath<BuildAbilityDefinition>("Assets/_Project/Tests/Fixtures/TwoCivDogfood/Build.asset");
        foreach (string name in new[] { "DefenseTower", "ResourceDepot", "Barracks" })
        {
            var definition = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/_Project/Data/Definitions/Buildings/" + name + ".asset");
            Check(definition != null && definition.basedSO != null, name + " definition/base load");
            if (definition == null || definition.basedSO == null) continue;
            var data = definition.basedSO;
            Check(registry.Buildings.Count(b => b == definition) == 1, name + " registered once");
            Check(build != null && build.Buildings.Count(b => b == definition) == 1, name + " worker build offer once");
            Check(data.Icon != null, name + " portrait/icon loads");
            var prefab = data.Prefab;
            Check(prefab != null, name + " prefab loads");
            if (prefab == null) continue;
            Check(prefab.GetComponent<BuildingAuthoring>()?.buildingDef == definition, name + " root definition binding");
            Check(prefab.GetComponent<GridSnapperAuthoring>() != null && prefab.GetComponent<BoxCollider>() != null, name + " placement footprint/collider");
            int expectedCells = name == "DefenseTower" ? 3 : name == "ResourceDepot" ? 4 : 6;
            Check(prefab.GetComponent<GridSnapperAuthoring>()?.GridScale == expectedCells, name + " compact footprint");
            Check(prefab.GetComponent<SingleSelectable>() != null, name + " root selectable");
            Check(prefab.GetComponentsInChildren<UnitAuthoring>(true).Length == 0, name + " no unit/building authoring conflict");
            Check(prefab.GetComponentsInChildren<MonoBehaviour>(true).All(component => component != null), name + " no missing nested scripts");
            if (name == "DefenseTower")
            {
                Check(prefab.GetComponent<StationaryDefenseAuthoring>() != null, "Tower aims without rotating grid collider");
                var attack = data.Abilities.OfType<AttackAbilityDefinition>().SingleOrDefault();
                Check(attack != null && attack.Weapons.Count == 2, "Tower retains two weapons");
                if (attack != null) foreach (var weapon in attack.Weapons)
                {
                    var projectile = weapon.ProjectilePrefab;
                    Check(projectile != null && projectile.GetComponent<ArtilleryBulletAuthoring>() != null &&
                        projectile.GetComponent<UnitAuthoring>() == null && projectile.GetComponent<Collider>() == null,
                        "Tower projectile has damage behavior without unit definition/ground blocker");
                }
            }
            if (name == "ResourceDepot")
                Check(data.Abilities.OfType<StorageAbilityDefinition>().Any(s => s.AcceptedResourceTypes.Contains(ResourceType.Gold) &&
                    s.AcceptedResourceTypes.Contains(ResourceType.Wood) && s.AcceptedResourceTypes.Contains(ResourceType.Food)), "Depot accepts existing three resources");
            if (name == "Barracks")
                Check(data.Jobs.Count == 1 && data.Jobs[0] is Train train && train.Id == "dogfood.train.soldier" &&
                    prefab.GetComponent<ProductionAuthoring>()?.SpawnOffset != null && prefab.GetComponent<ProductionAuthoring>()?.RallyOffset != null &&
                    prefab.GetComponent<ProductionAuthoring>().offsetsFollowModel,
                    "Barracks soldier offer and spawn/rally bindings");
        }
        Directory.CreateDirectory("Temp");
        text.AppendLine($"\nErrors: {errors}");
        File.WriteAllText("Temp/BuildingIntegrationReport.md", text.ToString());
        if (errors == 0) Debug.Log("BUILDING_CONTENT_VERIFICATION_PASS: Temp/BuildingIntegrationReport.md");
        else Debug.LogError($"Building content verification: {errors} errors; see Temp/BuildingIntegrationReport.md");
        return errors == 0;
    }
}
