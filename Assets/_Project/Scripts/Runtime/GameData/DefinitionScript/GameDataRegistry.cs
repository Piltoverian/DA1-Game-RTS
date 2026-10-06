using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;
using Unity.Collections;

[CreateAssetMenu(menuName = "ScriptableObjects/Game Data Registry")]
public class GameDataRegistry : ScriptableObject
{
    public List<UnitSO> Units = new();
    public List<BuildingSO> Buildings = new();
    public List<AbilityDefinition> Abilities = new();
    public List<Job> Jobs = new();

    public List<CivDef> Civs = new();

    public Sprite GetIcon<T>(FixedString64Bytes ID) where T: ScriptableObject
    {
        Sprite result;
        switch(typeof(T))
        {
            case Type t when t == typeof(UnitSO):
                result = Units?.FirstOrDefault(u => u != null && u.basedSO != null && u.basedSO.ID == ID)?.basedSO.Icon;
                break;
            case Type t when t == typeof(BuildingSO):
                result = Buildings?.FirstOrDefault(b => b != null && b.basedSO != null && b.basedSO.ID == ID)?.basedSO.Icon;
                break;
            case Type t when t == typeof(Job):
                result = Jobs?.FirstOrDefault(j => j != null && j.Id == ID)?.Icon;
                break;
            case Type t when t == typeof(CivDef):
                result = Civs?.FirstOrDefault(c => c != null && c.Id == ID)?.Icon;
                break;
            default:
                throw new NotSupportedException($"Unknown type: {typeof(T)}");
        }
        return result;
    }   

    public string GetName<T>(FixedString64Bytes ID) where T: ScriptableObject
    {
        string result;
        switch (typeof(T))
        {
            case Type t when t == typeof(UnitSO):
                result = Units?.FirstOrDefault(u => u != null && u.basedSO != null && u.basedSO.ID == ID)?.basedSO.Name;
                break;
            case Type t when t == typeof(BuildingSO):
                result = Buildings?.FirstOrDefault(b => b != null && b.basedSO != null && b.basedSO.ID == ID)?.basedSO.Name;
                break;
            case Type t when t == typeof(Job):
                result = Jobs?.FirstOrDefault(j => j != null && j.Id == ID)?.name;
                break;
            case Type t when t == typeof(CivDef):
                result = Civs?.FirstOrDefault(c => c != null && c.Id == ID)?.name;
                break;
            default:
                throw new NotSupportedException($"Unknown type: {typeof(T)}");
        }
        return result;
    }

    public Sprite GetIcon(FixedString64Bytes ID)
    {

        var unit = Units?.FirstOrDefault(u => u != null && u.basedSO != null && u.basedSO.ID == ID);
        if (unit != null) return unit.basedSO.Icon;

        var bld = Buildings?.FirstOrDefault(b => b != null && b.basedSO != null && b.basedSO.ID == ID);
        if (bld != null) return bld.basedSO.Icon;

        var job = Jobs?.FirstOrDefault(j => j != null && j.Id == ID);
        if (job != null) return job.Icon;

        var civ = Civs?.FirstOrDefault(c => c != null && c.Id == ID);
        if (civ != null) return civ.Icon;

        return null;
    }

    public string GetName(FixedString64Bytes ID)
    {

        var unit = Units?.FirstOrDefault(u => u != null && u.basedSO != null && u.basedSO.ID == ID);
        if (unit != null) return string.IsNullOrEmpty(unit.basedSO.Name) ? unit.name : unit.basedSO.Name;

        var bld = Buildings?.FirstOrDefault(b => b != null && b.basedSO != null && b.basedSO.ID == ID);
        if (bld != null) return string.IsNullOrEmpty(bld.basedSO.Name) ? bld.name : bld.basedSO.Name;

        var job = Jobs?.FirstOrDefault(j => j != null && j.Id == ID);
        if (job != null) return job.name;

        var civ = Civs?.FirstOrDefault(c => c != null && c.Id == ID);
        if (civ != null) return civ.name;

        return ID.ToString();
    }

    public BuildingSO GetBuildingSO(FixedString64Bytes ID)
    {
        if (Buildings == null) return null;
        return Buildings.FirstOrDefault(b => b != null && b.basedSO != null && b.basedSO.ID == ID);
    }

    public GameObject GetBuildingPrefab(FixedString64Bytes ID)
    {
        return Buildings?.FirstOrDefault(b => b != null && b.basedSO != null && b.basedSO.ID == ID)?.basedSO.Prefab;
    }

}
