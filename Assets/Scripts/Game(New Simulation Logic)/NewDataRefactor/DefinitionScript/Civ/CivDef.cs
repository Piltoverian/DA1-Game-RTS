using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "CivDef", menuName = "Game/CivDef", order = 1)]
public class CivDef : ScriptableObject
{
    public string Id;
    public string Name;

    public Sprite Icon;

    public TechTreeDef TechTree;

    public List<CivUnitUnlockEntry> UnitUnlocks = new();

    public GameObject TownHallPrefab;

    public GameObject StartWorker;

    private void OnValidate()
    {
        // Incomplete assets are checked by the registry validator, not on every edit.
        if (StartWorker == null || UnitUnlocks == null) return;
        bool foundStartWorker = false;

        for (int i = 0; i < UnitUnlocks.Count; i++)
        {
            var entry = UnitUnlocks[i];
            if (entry?.unitDefinition != null && entry.unitDefinition.basedSO != null &&
                entry.unitDefinition.basedSO.Prefab == StartWorker)
            {
                foundStartWorker = true;
                break;
            }
        }

        if (!foundStartWorker)
        {
            Debug.LogError("No start worker found in UnitUnlocks.", this);
        }
    }
}
