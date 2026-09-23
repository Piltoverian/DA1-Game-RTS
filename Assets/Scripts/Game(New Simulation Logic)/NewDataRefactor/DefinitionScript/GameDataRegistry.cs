using UnityEngine;
using System.Collections.Generic;
using System;

[CreateAssetMenu(menuName = "ScriptableObjects/Game Data Registry")]
public class GameDataRegistry : ScriptableObject
{
    public List<UnitSO> Units = new();
    public List<BuildingSO> Buildings = new();
    public List<AbilityDefinition> Abilities = new();
    public List<Job> Jobs = new();

    public List<CivDef> Civs = new();
}