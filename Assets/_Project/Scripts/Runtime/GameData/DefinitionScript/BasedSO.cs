using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewBasedSO", menuName = "ScriptableObjects/BasedSO", order = 1)]
public class BasedSO : ScriptableObject
{
    public string ID;
    public string Name;
    [Min(0.001f)] public float MaxHP = 100f;
    public Sprite Icon;
    public GameObject Prefab;

    [Min(0.001f)] public float WorkRate = 1f;
    [Min(1)] public int JobCapacity = 5;
    public List<AbilityDefinition> Abilities = new();
    public List<Job> Jobs = new();
}
