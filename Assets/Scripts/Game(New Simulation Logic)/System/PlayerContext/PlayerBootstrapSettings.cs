using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerBootstrapSettings", menuName = "Game/Player Bootstrap Settings")]
public class PlayerBootstrapSettings : ScriptableObject
{
    [System.Serializable]
    public class ResourceEntry
    {
        public GameObject Prefab;
        [Min(1)] public int CountPerPlayer = 1;
        [Min(1)] public int MinDistanceCells = 10;
        [Min(1)] public int AmountPerMine = 1500;
    }
    public Age StartingAge = Age.Industrial;
    [Min(0)] public int StartingWorkerCount = 5;
    [Min(1)] public int PlacementRadiusCells = 6;
    public List<ResourcePair> StartingResources = new();
    public List<ResourceEntry> StartingNodes = new();
}
