using UnityEngine;
using System.Collections.Generic;
[System.Serializable]
public class CivUnitUnlockEntry
{
   public UnitSO unitDefinition;
   public List<TechDefinition> Prerequisites = new ();
}
