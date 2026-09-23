using UnityEngine;
using System.Collections.Generic;

public enum UnitType
{
    Infantry = 0, Cavalry = 1, Archer = 2, Siege = 3, Naval = 4, Air = 5
}

[CreateAssetMenu(fileName = "NewUnitSO", menuName = "ScriptableObjects/UnitSO", order = 2)]
public class UnitSO : ScriptableObject
{
    public BasedSO basedSO;
    [Min(1)] public int PopulationCost = 1;
    public List<ResourcePair> ResourceCosts = new();
    public List<UnitType> UnitTypes = new();
}
