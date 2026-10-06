using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Abilities/Gather")]
public class GatherAbilityDefinition : AbilityDefinition
{
    [Min(1)] public int Capacity = 10;
    [Min(0.001f)] public float GatherTime = 2f;
    [Min(0)] public float StopDistance = 1.5f;
    [Min(1)] public float GatherRate = 1f;
}
