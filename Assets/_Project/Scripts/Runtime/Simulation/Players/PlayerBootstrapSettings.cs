using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerBootstrapSettings", menuName = "Game/Player Bootstrap Settings")]
public class PlayerBootstrapSettings : ScriptableObject
{
    public Age StartingAge = Age.Industrial;
    [Min(0)] public int StartingWorkerCount = 5;
    [Min(1)] public int PlacementRadiusCells = 6;
    [Min(0)] public float JoinTimeoutSeconds = 30;
    [Min(1)] public float LoadingTimeoutSeconds = 60;
    public List<ResourcePair> StartingResources = new();
}
