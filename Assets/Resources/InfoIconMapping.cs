using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;


public enum StatInfo//Some kind of stat do not have icon
{
    MaxHealth,
    Damage,
    Armor
}
[CreateAssetMenu(fileName = "InfoIconMapping", menuName = "ScriptableObjects/InfoIconMapping", order = 1)]
public class InfoIconMapping : ScriptableObject
{
    [Tooltip("Mapping of Stat and its Icon")]
    [SerializeField] private List<StatInfoPair> statInfoPairs;
    [Tooltip("Mapping of ResourceType and its Icon")]
    [SerializeField] private List<ResourceInfoPair> resourceInfoPairs;
    public Sprite GetIconForStat(StatInfo statInfo)
    {
        foreach (var pair in statInfoPairs)
        {
            if (pair.StatInfo == statInfo) return pair.Icon;
        }
        Debug.LogWarning($"No icon found for StatInfo: {statInfo}");
        return null;
    }

    public Sprite GetIconForResourceType(ResourceType resourceType)
    {
        foreach (var pair in resourceInfoPairs)
        {
            if (pair.ResourceType == resourceType) return pair.Icon;
        }
        Debug.LogWarning($"No icon found for ResourceType: {resourceType}");
        return null;
    }
}
[System.Serializable]
struct StatInfoPair
{
    public StatInfo StatInfo;
    public Sprite Icon;
}

[System.Serializable]
struct ResourceInfoPair
{
    public ResourceType ResourceType;
    public Sprite Icon;
}