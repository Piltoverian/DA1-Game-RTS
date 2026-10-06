using UnityEngine;
using System.Collections.Generic;

public enum BuildingTags
{
    Depot = 0, Barracks = 1, Tower = 2, House = 3, TownHall = 4
}

[CreateAssetMenu(fileName = "NewBuildingSO", menuName = "ScriptableObjects/BuildingSO", order = 3)]
public class BuildingSO : ScriptableObject
{
    public BasedSO basedSO;
    public List<BuildingTags> Tags = new();
    public List<ResourcePair> Cost = new();
    [Min(0)] public int PopulationCapacity;
    [Min(0.001f)] public float WorkLoad = 100f;
}
