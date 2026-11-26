using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using Unity.Entities.Hybrid.Baking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayableDemoSetup
{
    [MenuItem("Tools/RTS UI/Prepare 1v1 playable demo")]
    public static void Prepare()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode before preparing content.");
        const string dataFolder = "Assets/_Project/Data/Playable";
        const string resourceFolder = "Assets/_Project/Prefabs/Resources";
        EnsureFolder(dataFolder); EnsureFolder(resourceFolder);
        var wood = ResourcePrefab(resourceFolder + "/TimberGrove.prefab", "Timber Grove", ResourceType.Wood, "Assets/_Project/Art/Models/Nature/tree1.fbx");
        var food = ResourcePrefab(resourceFolder + "/FoodBush.prefab", "Food Bush", ResourceType.Food, "Assets/_Project/Art/Models/Nature/bush1.fbx");
        const string profilePath = dataFolder + "/SkirmishMap.asset";
        if (!AssetDatabase.LoadAssetAtPath<MapGenConfig>(profilePath)) AssetDatabase.CopyAsset("Assets/_Project/Tests/Fixtures/MapGeneration/MapGenConfig.asset", profilePath);
        var config = AssetDatabase.LoadAssetAtPath<MapGenConfig>(profilePath);
        config.MapSize = GridAuthoring.MapType.Medium; config.MinimumCellSize = 1;
        config.Terrain.players = 2; config.Terrain.protectedRadius = 16; config.Terrain.mountainDensity = .32f;
        config.Resources.EnsureMainResourceVariety = true;
        config.Resources.Catalog = new List<MapResourceDefinition>
        {
            new MapResourceDefinition { Id = "gold.common", Type = ResourceType.Gold, Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Tests/Fixtures/MapGeneration/TempGold.prefab"), AmountPerMine = 1500 },
            new MapResourceDefinition { Id = "wood.common", Type = ResourceType.Wood, Prefab = wood, AmountPerMine = 1800 },
            new MapResourceDefinition { Id = "food.common", Type = ResourceType.Food, Prefab = food, AmountPerMine = 1200 }
        };
        config.Resources.MainClusters = 1; config.Resources.SecondaryClusters = 2;
        config.Resources.AdvantageClusters = 1; config.Resources.ContestedClusters = 2;
        config.Resources.Main.MinesPerCluster = 3; config.Resources.Main.DistanceCells = 12;
        config.Resources.CenterAttempts = 48; config.Resources.MemberAttempts = 96;
        EditorUtility.SetDirty(config);
        const string settingsPath = dataFolder + "/SkirmishStart.asset";
        if (!AssetDatabase.LoadAssetAtPath<PlayerBootstrapSettings>(settingsPath)) AssetDatabase.CopyAsset("Assets/_Project/Tests/Fixtures/MapGeneration/BootstrapSettings.asset", settingsPath);
        var settings = AssetDatabase.LoadAssetAtPath<PlayerBootstrapSettings>(settingsPath);
        settings.StartingWorkerCount = 5; settings.PlacementRadiusCells = 20;
        settings.StartingResources = new List<ResourcePair> { new ResourcePair(ResourceType.Gold, 350), new ResourcePair(ResourceType.Wood, 350), new ResourcePair(ResourceType.Food, 250) };
        EditorUtility.SetDirty(settings); config.PlayerBootstrap = settings; EditorUtility.SetDirty(config);
        const string subPath = "Assets/_Project/Scenes/Main/EntitySubscene.unity";
        var subScene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(subPath);
        bool openedHere = !subScene.isLoaded;
        if (openedHere) subScene = EditorSceneManager.OpenScene(subPath, OpenSceneMode.Additive);
        foreach (var root in subScene.GetRootGameObjects())
        {
            foreach (var grid in root.GetComponentsInChildren<GridAuthoring>(true))
            { grid.Config = config; grid.transform.localScale = new Vector3(25, 1, 25); EditorUtility.SetDirty(grid); }
            foreach (var bootstrap in root.GetComponentsInChildren<PlayerBootstrapAuthoring>(true))
            {
                bootstrap.MapConfig = config; bootstrap.Settings = settings;
                bootstrap.Players.RemoveAll(player => player.PlayerId != 1 && player.PlayerId != 2);
                EditorUtility.SetDirty(bootstrap);
            }
        }
        EditorSceneManager.MarkSceneDirty(subScene); EditorSceneManager.SaveScene(subScene);
        if (openedHere) EditorSceneManager.CloseScene(subScene, true);
        const string uiPath = "Assets/_Project/Prefabs/UI prefab/UICanvas.prefab";
        var canvas = PrefabUtility.LoadPrefabContents(uiPath);
        try
        {
            var controller = canvas.GetComponent<PlayableSessionController>() ?? canvas.AddComponent<PlayableSessionController>();
            var fields = new SerializedObject(controller);
            fields.FindProperty("textTemplate").objectReferenceValue = canvas.GetComponentsInChildren<TextMeshProUGUI>(true).First();
            fields.FindProperty("buttonPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI prefab/NonNavigationButton.prefab");
            fields.FindProperty("panelSkin").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/Sprites/XianxiaPanelV2.png");
            fields.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(canvas, uiPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(canvas); }
        const string hallPath = "Assets/_Project/Tests/Fixtures/TwoCivDogfood/TownHall.prefab";
        var hall = PrefabUtility.LoadPrefabContents(hallPath);
        try { hall.GetComponent<ProductionAuthoring>().offsetsFollowModel = true; PrefabUtility.SaveAsPrefabAsset(hall, hallPath); }
        finally { PrefabUtility.UnloadPrefabContents(hall); }
        foreach (string name in new[] { "DefenseTower", "ResourceDepot", "Barracks" })
        {
            var definition = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/_Project/Data/Definitions/Buildings/" + name + ".asset");
            definition.basedSO.MaxHP = name == "DefenseTower" ? 500 : name == "ResourceDepot" ? 450 : 700;
            if (name == "ResourceDepot") { definition.WorkLoad = 40; definition.Cost = new List<ResourcePair> { new ResourcePair(ResourceType.Wood, 100) }; }
            EditorUtility.SetDirty(definition); EditorUtility.SetDirty(definition.basedSO);
        }
        foreach (string name in new[] { "TownHallBase", "SoldierBase" })
        {
            var definition = AssetDatabase.LoadAssetAtPath<BasedSO>("Assets/_Project/Tests/Fixtures/TwoCivDogfood/" + name + ".asset");
            definition.Name = name == "TownHallBase" ? "TownHall" : "Soldier"; EditorUtility.SetDirty(definition);
        }
        var train = AssetDatabase.LoadAssetAtPath<Train>("Assets/_Project/Tests/Fixtures/TwoCivDogfood/TrainSoldier.asset");
        train.WorkLoad = 15; EditorUtility.SetDirty(train);
        foreach (string attackPath in new[] { "Assets/_Project/Data/Definitions/SoldierAttackDef.asset", "Assets/_Project/Data/Definitions/Buildings/DefenseTowerAttack.asset" })
        {
            var attack = AssetDatabase.LoadAssetAtPath<AttackAbilityDefinition>(attackPath);
            for (int i = 0; i < attack.Weapons.Count; i++)
            { var weapon = attack.Weapons[i]; weapon.ProjectileSpeed = attackPath.Contains("DefenseTower") ? 20 : 28; attack.Weapons[i] = weapon; }
            EditorUtility.SetDirty(attack);
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Directory.CreateDirectory("Temp/PlayableLoop"); File.WriteAllText("Temp/PlayableLoop/setup-result.txt", "Prepared 1v1: 128x128 grid, 250 world units, balanced main resource types, paid AI, match results/restart.");
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }
    private static GameObject ResourcePrefab(string path, string title, ResourceType type, string modelPath)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing != null) return existing;
        var root = new GameObject(title);
        try
        {
            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
            model.transform.SetParent(root.transform, false);
            var bounds = ModelSnapSource.CaptureVisualBounds(root, new Bounds(Vector3.zero, Vector3.one));
            var collider = root.AddComponent<BoxCollider>(); collider.center = bounds.center; collider.size = Vector3.Max(bounds.size, Vector3.one * .1f);
            root.AddComponent<ResourceAuthoring>().ResourceType = type;
            root.AddComponent<GridSnapperAuthoring>().GridScale = 1;
            root.AddComponent<ResourceNodeReference>(); root.AddComponent<LinkedEntityGroupAuthoring>();
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { Object.DestroyImmediate(root); }
    }
}
