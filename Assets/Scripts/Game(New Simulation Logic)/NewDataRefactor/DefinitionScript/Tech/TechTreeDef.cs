using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TechTreeDef", menuName = "Game/TechTreeDef", order = 1)]
public class TechTreeDef : ScriptableObject
{
    public string Id;

    public List<TechTreeNode> Nodes = new();
}
