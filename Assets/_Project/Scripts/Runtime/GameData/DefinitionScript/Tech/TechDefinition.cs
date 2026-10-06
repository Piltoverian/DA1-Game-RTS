using UnityEngine;
using System.Collections.Generic;
using Unity.Collections;

[CreateAssetMenu(fileName = "NewTech", menuName = "ScriptableObjects/Tech", order = 5)]
public class TechDefinition : ScriptableObject
{
    public FixedString64Bytes IDs;
    public List<ResourcePair> Cost = new();
}
