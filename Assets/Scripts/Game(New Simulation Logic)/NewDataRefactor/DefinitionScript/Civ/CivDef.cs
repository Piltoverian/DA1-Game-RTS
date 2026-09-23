using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "CivDef", menuName = "Game/CivDef", order = 1)]
public class CivDef : ScriptableObject
{
    public string Id;
    public string Name;

    public Sprite Icon;

    public TechTreeDef TechTree;

    public List<CivUnitUnlockEntry> UnitUnlocks = new();
}
