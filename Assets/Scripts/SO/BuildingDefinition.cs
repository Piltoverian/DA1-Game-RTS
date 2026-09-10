using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "RTS/Building Placement Definition")]
public class BuildingDefinition : ScriptableObject
{
    [Header("Display")]
    public string DisplayName = "Build Building";
    public Sprite Icon;

    [Header("Category")]
    public BuildingType BuildingType;

    [Header("Cost & Workload")]
    public float TotalWorkLoad = 100f;
    public List<ResourcePair> Cost;

    [Header("Prefab & Preview")]
    public GameObject BuildingPrefab;
    public GameObject PreviewPrefab;
}