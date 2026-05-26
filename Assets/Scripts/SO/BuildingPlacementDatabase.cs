using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "RTS/Building Placement Database")]
public class BuildingPlacementDatabase : ScriptableObject
{
    public List<BuildingPlacementDefinition> Buildings;

    public BuildingPlacementDefinition GetByCommandIndex(int index)
    {
        foreach (var building in Buildings)
        {
            if (building != null && building.CommandIndex == index)
                return building;
        }

        return null;
    }

    public BuildingPlacementDefinition GetByType(BuildingType type)
    {
        foreach (var building in Buildings)
        {
            if (building != null && building.BuildingType == type)
                return building;
        }

        return null;
    }
}