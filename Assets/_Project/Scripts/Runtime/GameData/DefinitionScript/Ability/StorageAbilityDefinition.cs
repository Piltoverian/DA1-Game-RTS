using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "ScriptableObjects/Abilities/Storage")]
public class StorageAbilityDefinition : AbilityDefinition
{
    public List<ResourceType> AcceptedResourceTypes = new()
    {
        ResourceType.Gold, ResourceType.Wood, ResourceType.Food
    };
}
