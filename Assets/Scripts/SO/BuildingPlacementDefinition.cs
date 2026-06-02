using UnityEngine;

[CreateAssetMenu(menuName = "RTS/Building Placement Definition")]
public class BuildingPlacementDefinition : ScriptableObject
{
    [Header("Command")]
    public int CommandIndex;

    [Header("Display")]
    public string DisplayName = "Build Building";

    [Header("Category")]
    public BuildingType BuildingType;

    [Header("Preview")]
    public GameObject PreviewPrefab;
}