
using UnityEngine;
using System.Collections.Generic;
[CreateAssetMenu(menuName = "ScriptableObjects/Abilities/Build")]
public class BuildAbilityDefinition : AbilityDefinition
{
    [Min(0)] public float Range = 2f;

    public List<BuildingSO> Buildings = new();
}
