using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "RTS/Building Placement Database")]
public class BuildingDatabase : ScriptableObject
{
    public List<BuildingDefinition> Buildings;

    public int GetIndexOf(BuildingDefinition definition)
    {
        if (Buildings != null && definition != null)
            return Buildings.IndexOf(definition);

        return -1;
    }

    public BuildingDefinition GetByIndex(int index)
    {
        if (Buildings != null && index >= 0 && index < Buildings.Count)
            return Buildings[index];

        return null;
    }

    public BuildingDefinition GetByCommandIndex(int index)
    {
        return GetByIndex(index);
    }

    public BuildingDefinition GetByType(BuildingType type)
    {
        foreach (var building in Buildings)
        {
            if (building != null && building.BuildingType == type)
                return building;
        }

        return null;
    }
}