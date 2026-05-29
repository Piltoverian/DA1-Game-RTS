using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CommandIconMapping", menuName = "ScriptableObjects/CommandIconMapping", order = 1)]
public class IconMapping : ScriptableObject
{
    [SerializeField] private List<IconPair> IconPairs;

    public Sprite GetIconOfCommand(string commandName)
    {
        IconPair pair = IconPairs.Find(p => p.commandName.Normalize() == commandName.Normalize());
        if (pair != null)
        {
            return pair.icon;
        }
        else
        {
            Debug.LogWarning($"No icon found for command: {commandName}");
            return null;
        }
    }
}

[System.Serializable]
public class IconPair
{
    public string commandName;
    public Sprite icon;
}
