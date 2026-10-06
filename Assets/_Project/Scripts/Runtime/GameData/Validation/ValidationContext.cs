using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RTS.DataValidation
{
    internal sealed class ValidationContext
    {
        internal readonly List<ValidationIssue> Issues = new();
        private readonly HashSet<ScriptableObject> visited = new();
        private readonly Dictionary<Type, Dictionary<string, ScriptableObject>> ids = new();
        private readonly HashSet<UnitSO> registeredUnits = new();
        private readonly HashSet<BuildingSO> registeredBuildings = new();
        private readonly HashSet<AbilityDefinition> registeredAbilities = new();
        private readonly HashSet<Job> registeredJobs = new();
        private readonly HashSet<TechDefinition> registeredTechs = new();
        private readonly Dictionary<TechDefinition, Research> researchByTech = new();
        private GameDataRegistry registry;

        internal bool IsRegisteredUnit(UnitSO unit) => registeredUnits.Contains(unit);
        internal bool IsRegisteredBuilding(BuildingSO building) => registeredBuildings.Contains(building);
        internal bool IsRegisteredAbility(AbilityDefinition ability) => registeredAbilities.Contains(ability);
        internal bool IsRegisteredJob(Job job) => registeredJobs.Contains(job);
        internal bool IsRegisteredTech(TechDefinition tech) => registeredTechs.Contains(tech);

        // Membership comes only from registration lists and the explicitly owned children.
        // Build all sets before validation so list order cannot change reference validity.
        private void PrepareMembership(GameDataRegistry registry)
        {
            this.registry = registry;
            AddMembers(registry.Units, registeredUnits);
            AddMembers(registry.Buildings, registeredBuildings);
            AddMembers(registry.Abilities, registeredAbilities);
            AddMembers(registry.Jobs, registeredJobs);
            var trees = new HashSet<TechTreeDef>();
            if (registry.Civs != null)
                foreach (var civ in registry.Civs)
                    if (civ != null && civ.TechTree != null) trees.Add(civ.TechTree);
            foreach (var tree in trees)
                if (tree.Nodes != null)
                    foreach (var node in tree.Nodes)
                        if (node != null && node.techDefinition != null)
                            registeredTechs.Add(node.techDefinition);

            // Iterate the list for stable diagnostics, but count each Research asset once.
            var seenResearch = new HashSet<Research>();
            if (registry.Jobs != null)
                foreach (var job in registry.Jobs)
                {
                    if (!(job is Research research) || !seenResearch.Add(research)) continue;
                    var tech = research.TechDefinition;
                    if (tech == null || !registeredTechs.Contains(tech)) continue;
                    if (researchByTech.TryGetValue(tech, out var existing))
                        Error(research, "techDefinition", "TECH_RESEARCH_AMBIGUOUS",
                            $"Tech '{tech.name}' is already referenced by Research '{existing.name}'. " +
                            "Register exactly one Research asset per Tech; producers may share that Research.", existing);
                    else researchByTech.Add(tech, research);
                }

            ValidateBaseIdentity(registry.Units, u => u.basedSO, "UNIT_BASE_IDENTITY_AMBIGUOUS");
            ValidateBaseIdentity(registry.Buildings, b => b.basedSO, "BUILDING_BASE_IDENTITY_AMBIGUOUS");
        }

        private static void AddMembers<T>(List<T> values, HashSet<T> members) where T : ScriptableObject
        {
            if (values == null) return;
            foreach (var value in values) if (value != null) members.Add(value);
        }

        private void ValidateBaseIdentity<T>(List<T> values, Func<T, BasedSO> getBase, string code)
            where T : ScriptableObject
        {
            if (values == null) return;
            var owners = new Dictionary<BasedSO, T>();
            var seen = new HashSet<T>();
            foreach (var value in values)
            {
                if (value == null || !seen.Add(value)) continue;
                var basis = getBase(value);
                if (basis == null) continue;
                if (owners.TryGetValue(basis, out var existing))
                    Error(value, "basedSO", code,
                        $"Base '{basis.name}' already supplies identity to '{existing.name}' of the same definition type.", existing);
                else owners.Add(basis, value);
            }
        }

        internal void RequireResearch(TechDefinition tech, TechTreeDef tree, string field)
        {
            if (tech != null && registeredTechs.Contains(tech) && !researchByTech.ContainsKey(tech))
            {
                var civNames = new List<string>();
                var seenCivs = new HashSet<CivDef>();
                if (registry.Civs != null)
                    foreach (var civ in registry.Civs)
                        if (civ != null && civ.TechTree == tree && seenCivs.Add(civ))
                            civNames.Add($"'{civ.name}' (Id: {civ.Id})");

                Error(tree, field, "TECH_RESEARCH_MISSING",
                    $"Tech '{tech.name}' appears in TechTree '{tree.name}' used by Civ(s) {string.Join(", ", civNames)}, " +
                    "but cannot be researched: no Research in GameDataRegistry.Jobs references this Tech. " +
                    "Register a Research with techDefinition pointing to this asset, or remove the Tech from the tree.", tech);
            }
        }

        internal void Error(Object asset, string field, string code, string message, Object related = null)
            => Issues.Add(new ValidationIssue(ValidationSeverity.Error, code, asset, field, message, related));

        internal void Warning(Object asset, string field, string code, string message)
            => Issues.Add(new ValidationIssue(ValidationSeverity.Warning, code, asset, field, message));

        internal bool Required(Object value, Object asset, string field)
        {
            if (value != null) return true;
            Error(asset, field, "REFERENCE_REQUIRED", "Reference is required.");
            return false;
        }

        internal bool List<T>(List<T> values, Object asset, string field)
        {
            if (values != null) return true;
            Error(asset, field, "LIST_NULL", "List must not be null; use an empty list when appropriate.");
            return false;
        }

        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal void Number(float value, float minimum, Object asset, string field, bool exclusive = false)
        {
            if (!Finite(value) || (exclusive ? value <= minimum : value < minimum))
                Error(asset, field, "NUMBER_RANGE", $"Expected a finite value {(exclusive ? ">" : ">=")} {minimum}.");
        }

        internal void Id(ScriptableObject asset, string value, string field, Type scope)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Error(asset, field, "ID_REQUIRED", "ID must not be empty or whitespace.");
                return;
            }
            if (!ids.TryGetValue(scope, out var table))
                ids.Add(scope, table = new Dictionary<string, ScriptableObject>(StringComparer.Ordinal));
            if (table.TryGetValue(value, out var existing) && existing != asset)
                Error(asset, field, "ID_DUPLICATE", $"ID '{value}' in scope {scope.Name} is also used by '{existing.name}'.", existing);
            else table[value] = asset;
        }

        internal void Reference(ScriptableObject value, Object asset, string field)
        {
            if (Required(value, asset, field)) Visit(value);
        }

        internal void RegisteredReference<T>(T value, Object asset, string field,
            Func<T, bool> isMember, string code, string registration) where T : ScriptableObject
        {
            if (!Required(value, asset, field)) return;
            if (!isMember(value))
            {
                Error(asset, field, code, $"'{value.name}' must be registered in {registration}.", value);
                return;
            }
            Visit(value);
        }

        internal void References<T>(List<T> values, Object asset, string field,
            Func<T, bool> isMember = null, string code = null, string registration = null) where T : ScriptableObject
        {
            if (!List(values, asset, field)) return;
            var local = new HashSet<T>();
            for (int i = 0; i < values.Count; i++)
            {
                var value = values[i];
                var path = $"{field}[{i}]";
                if (!Required(value, asset, path)) continue;
                if (!local.Add(value))
                    Error(asset, path, "REFERENCE_DUPLICATE", "The same asset appears more than once in this list.", value);
                if (isMember == null) Visit(value);
                else RegisteredReference(value, asset, path, isMember, code, registration);
            }
        }

        internal void Visit(ScriptableObject asset)
        {
            if (asset == null || !visited.Add(asset)) return;
            switch (asset)
            {
                case GameDataRegistry registry:
                    PrepareMembership(registry);
                    References(registry.Units, registry, nameof(registry.Units));
                    References(registry.Buildings, registry, nameof(registry.Buildings));
                    References(registry.Abilities, registry, nameof(registry.Abilities));
                    References(registry.Jobs, registry, nameof(registry.Jobs));
                    References(registry.Civs, registry, nameof(registry.Civs));
                    break;
                case CivDef civ:
                    CivValidator.Validate(civ, this);
                    break;
                case TechTreeDef tree:
                    Id(tree, tree.Id, nameof(tree.Id), typeof(TechTreeDef));
                    TechTreeValidator.Validate(tree, this);
                    break;
                default:
                    DefinitionValidator.Validate(asset, this);
                    break;
            }
        }
    }
}
