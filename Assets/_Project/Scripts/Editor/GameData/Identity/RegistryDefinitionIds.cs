using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RTS.DataEditor
{
    // Editor-only: never scan or mutate definitions outside the selected registry graph.
    internal static class RegistryDefinitionIds
    {
        internal static HashSet<ScriptableObject> Collect(GameDataRegistry registry)
        {
            var visited = new HashSet<ScriptableObject>();
            var pending = new Stack<ScriptableObject>();
            void Add(ScriptableObject asset) { if (asset != null) pending.Push(asset); }
            void AddRange<T>(List<T> assets) where T : ScriptableObject
            {
                if (assets != null) foreach (var asset in assets) Add(asset);
            }
            Add(registry);
            while (pending.Count > 0)
            {
                var asset = pending.Pop();
                if (!visited.Add(asset)) continue;
                switch (asset)
                {
                    case GameDataRegistry r:
                        AddRange(r.Units); AddRange(r.Buildings); AddRange(r.Abilities);
                        AddRange(r.Jobs); AddRange(r.Civs); break;
                    case UnitSO u: Add(u.basedSO); break;
                    case BuildingSO b: Add(b.basedSO); break;
                    case BasedSO b: AddRange(b.Abilities); AddRange(b.Jobs); break;
                    case Train t: Add(t.OutputUnit); break;
                    case Research r: Add(r.TechDefinition); break;
                    case CivDef c: Add(c.TechTree); break;
                    case TechTreeDef t:
                        if (t.Nodes != null) foreach (var node in t.Nodes)
                        {
                            if (node == null) continue;
                            Add(node.techDefinition); AddRange(node.Prerequisites);
                        }
                        break;
                }
            }
            return visited;
        }

        private static string Field(ScriptableObject asset) => asset switch
        {
            BasedSO _ => nameof(BasedSO.ID),
            AbilityDefinition _ => nameof(AbilityDefinition.Id),
            Job _ => nameof(Job.Id),
            CivDef _ => nameof(CivDef.Id),
            TechTreeDef _ => nameof(TechTreeDef.Id),
            _ => null
        };

        internal static int AssignMissing(GameDataRegistry registry,
            HashSet<ScriptableObject> previouslyRegistered = null)
        {
            var graph = Collect(registry);
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var asset in graph)
            {
                var field = Field(asset);
                if (field != null) used.Add(new SerializedObject(asset).FindProperty(field).stringValue);
            }
            int count = 0;
            foreach (var asset in graph)
            {
                // Base identity is assigned only by the explicit registry button.
                // Preserve automatic assignment for other newly registered definitions.
                if (previouslyRegistered != null && asset is BasedSO) continue;
                if (previouslyRegistered != null && previouslyRegistered.Contains(asset)) continue;
                var field = Field(asset);
                if (field == null) continue;
                var serialized = new SerializedObject(asset);
                var property = serialized.FindProperty(field);
                if (!string.IsNullOrWhiteSpace(property.stringValue)) continue;
                string id;
                do { id = Guid.NewGuid().ToString("N"); } while (!used.Add(id));
                // ApplyModifiedProperties records Undo and marks the asset dirty.
                property.stringValue = id;
                serialized.ApplyModifiedProperties();
                count++;
            }
            return count;
        }
    }
}
