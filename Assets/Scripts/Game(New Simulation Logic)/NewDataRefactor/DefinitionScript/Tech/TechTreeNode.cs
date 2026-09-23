using UnityEngine;
using System.Collections.Generic;
[System.Serializable]
public class TechTreeNode 
{
    public TechDefinition techDefinition;

    public List<TechDefinition> Prerequisites = new();

    public Vector2 Position;
}
