using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RTS.DataValidation;
using UnityEngine;

static class Program
{
    static int count;
    static int Main()
    {
        Run("empty registry", () => Clean(new GameDataRegistry()));
        Run("null registry", () => Has(GameDataValidator.Validate(null), "REFERENCE_REQUIRED"));
        Run("null registration lists", () => {
            var r = new GameDataRegistry { Units = null, Buildings = null, Abilities = null, Jobs = null, Civs = null };
            Equal(5, GameDataValidator.Validate(r).Count(x => x.Code == "LIST_NULL"));
        });
        Run("valid fixture and read-only", () => {
            var f = new Fixture();
            var before = Snapshot(f.Registry);
            Clean(f.Registry);
            Equal(before, Snapshot(f.Registry));
        });
        Run("unregistered ability with same ID", () => {
            var f = new Fixture();
            var outside = new GatherAbilityDefinition { Id = f.Gather.Id, name = "outside" };
            f.Base.Abilities[0] = outside;
            var issue = Has(f.Issues(), "BASE_ABILITY_NOT_IN_REGISTRY");
            Equal(f.Base, issue.Asset); Equal(outside, issue.RelatedAsset); Equal("Abilities[0]", issue.FieldPath);
        });
        Run("unregistered job does not bring in its output", () => {
            var f = new Fixture();
            f.Registry.Jobs.Remove(f.Train);
            f.Train.OutputUnit = new UnitSO();
            var issues = f.Issues();
            Has(issues, "BASE_JOB_NOT_IN_REGISTRY");
            Assert(!issues.Any(x => x.Asset == f.Train.OutputUnit), "outside unit was traversed");
        });
        Run("train requires direct Unit registration", () => {
            var f = new Fixture(); f.Registry.Units.Clear();
            Has(f.Issues(), "TRAIN_UNIT_NOT_IN_REGISTRY"); Has(f.Issues(), "CIV_UNIT_NOT_IN_REGISTRY");
            Equal(0, f.Registry.Units.Count);
        });
        Run("research outside tree", () => {
            var f = new Fixture(); SetTech(f.Research, new TechDefinition { name = "outside" });
            Has(f.Issues(), "RESEARCH_TECH_NOT_IN_CATALOG"); Has(f.Issues(), "TECH_RESEARCH_MISSING");
        });
        Run("research without registered Civ", () => {
            var f = new Fixture(); f.Registry.Civs.Clear(); Has(f.Issues(), "RESEARCH_TECH_NOT_IN_CATALOG");
        });
        Run("tech requires registered Research", () => {
            var f = new Fixture(); f.Registry.Jobs.Remove(f.Research);
            var issue = Has(f.Issues(), "TECH_RESEARCH_MISSING");
            Equal(f.Tree, issue.Asset); Equal(f.Tech, issue.RelatedAsset); Equal("Nodes[0].techDefinition", issue.FieldPath);
        });
        Run("missing Research identifies Tech tree and Civ", () => {
            var f = new Fixture(); f.Tree.name = "Industry Tree"; f.Civ.name = "Nation A";
            f.Registry.Jobs.Remove(f.Research);
            var issue = Has(f.Issues(), "TECH_RESEARCH_MISSING");
            Equal(ValidationSeverity.Error, issue.Severity);
            Assert(issue.Message.Contains("Tech 'tech'") && issue.Message.Contains("Industry Tree") &&
                issue.Message.Contains("Nation A") && issue.Message.Contains("cannot be researched"), issue.Message);
            f.Registry.Jobs.Add(f.Research); Lacks(f.Issues(), "TECH_RESEARCH_MISSING");
        });
        Run("shared tree missing Research names all Civs once", () => {
            var f = new Fixture(); f.Civ.name = "Nation A";
            f.Registry.Civs.Add(new CivDef { Id = "civ-b", name = "Nation B", TechTree = f.Tree });
            f.Registry.Jobs.Remove(f.Research);
            var issues = f.Issues();
            Equal(1, issues.Count(x => x.Code == "TECH_RESEARCH_MISSING"));
            var issue = Has(issues, "TECH_RESEARCH_MISSING");
            Assert(issue.Message.Contains("Nation A") && issue.Message.Contains("Nation B"), issue.Message);
        });
        Run("missing Research in another Civ tree is detected", () => {
            var f = new Fixture(); var tech = new TechDefinition { name = "Other Tech" };
            var tree = new TechTreeDef { Id = "other-tree", name = "Other Tree" };
            tree.Nodes.Add(new TechTreeNode { techDefinition = tech });
            f.Registry.Civs.Add(new CivDef { Id = "other-civ", name = "Other Civ", TechTree = tree });
            var issue = Has(f.Issues(), "TECH_RESEARCH_MISSING");
            Equal(tree, issue.Asset); Equal(tech, issue.RelatedAsset);
            Assert(issue.Message.Contains("Other Civ"), issue.Message);
        });
        Run("two distinct Research jobs are ambiguous", () => {
            var f = new Fixture(); var other = new Research { Id = "research-2", name = "second research" };
            SetTech(other, f.Tech); f.Registry.Jobs.Add(other);
            var issue = Has(f.Issues(), "TECH_RESEARCH_AMBIGUOUS");
            Equal(other, issue.Asset); Equal(f.Research, issue.RelatedAsset); Equal("techDefinition", issue.FieldPath);
        });
        Run("repeated Research entry is list duplicate, not two jobs", () => {
            var f = new Fixture(); f.Registry.Jobs.Add(f.Research);
            Has(f.Issues(), "REFERENCE_DUPLICATE"); Lacks(f.Issues(), "TECH_RESEARCH_AMBIGUOUS");
        });
        Run("multiple producers share one Research", () => {
            var f = new Fixture();
            var b = new BasedSO { ID = "producer2", Prefab = new GameObject(), Jobs = new List<Job> { f.Research } };
            f.Registry.Buildings.Add(new BuildingSO { basedSO = b }); Clean(f.Registry);
        });
        Run("Research ID missing", () => { var f = new Fixture(); f.Research.Id = " "; Has(f.Issues(), "ID_REQUIRED"); });
        Run("Research WorkLoad invalid", () => { var f = new Fixture(); f.Research.WorkLoad = float.NaN; Has(f.Issues(), "NUMBER_RANGE"); });
        Run("Research Tech null", () => { var f = new Fixture(); SetTech(f.Research, null); Has(f.Issues(), "REFERENCE_REQUIRED"); Has(f.Issues(), "TECH_RESEARCH_MISSING"); });
        Run("two Techs with duplicate Research IDs", () => {
            var f = new Fixture(); f.AddTech("other", f.Research.Id); Has(f.Issues(), "ID_DUPLICATE");
        });
        Run("Train and Research may share text ID", () => { var f = new Fixture(); f.Train.Id = f.Research.Id; Clean(f.Registry); });
        Run("same Tech shared by two trees", () => {
            var f = new Fixture();
            var tree = new TechTreeDef { Id = "tree2", Nodes = new List<TechTreeNode> { new TechTreeNode { techDefinition = f.Tech } } };
            f.Registry.Civs.Add(new CivDef { Id = "civ2", TechTree = tree }); Clean(f.Registry);
        });
        Run("tree prerequisite outside own tree even if registered elsewhere", () => {
            var f = new Fixture(); var tech = f.AddTech("other", "research2");
            f.Tree.Nodes.RemoveAt(1);
            var tree = new TechTreeDef { Id = "tree2", Nodes = new List<TechTreeNode> { new TechTreeNode { techDefinition = tech } } };
            f.Registry.Civs.Add(new CivDef { Id = "civ2", TechTree = tree });
            f.Tree.Nodes[0].Prerequisites.Add(tech); Has(f.Issues(), "TREE_PREREQUISITE_OUTSIDE_TREE");
        });
        Run("prerequisite reference does not register Tech", () => {
            var f = new Fixture(); f.Tree.Nodes.Clear();
            f.Tree.Nodes.Add(new TechTreeNode { techDefinition = null, Prerequisites = new List<TechDefinition> { f.Tech } });
            Has(f.Issues(), "RESEARCH_TECH_NOT_IN_CATALOG"); Has(f.Issues(), "TREE_PREREQUISITE_OUTSIDE_TREE");
        });
        Run("Civ prerequisite outside tree", () => {
            var f = new Fixture(); f.Civ.UnitUnlocks[0].Prerequisites.Add(new TechDefinition());
            Has(f.Issues(), "CIV_PREREQUISITE_OUTSIDE_TREE");
        });
        Run("cycle still detected", () => {
            var f = new Fixture(); var t = f.AddTech("other", "research2");
            f.Tree.Nodes[0].Prerequisites.Add(t); f.Tree.Nodes[1].Prerequisites.Add(f.Tech);
            Has(f.Issues(), "TREE_CYCLE");
        });
        Run("shared Base identity for distinct Units rejected", () => {
            var f = new Fixture(); f.Registry.Units.Add(new UnitSO { basedSO = f.Base });
            Has(f.Issues(), "UNIT_BASE_IDENTITY_AMBIGUOUS");
        });
        Run("shared Base identity for distinct Buildings rejected", () => {
            var f = new Fixture(); var b = f.Registry.Buildings[0].basedSO;
            f.Registry.Buildings.Add(new BuildingSO { basedSO = b }); Has(f.Issues(), "BUILDING_BASE_IDENTITY_AMBIGUOUS");
        });
        Run("same Unit repeated does not produce identity ambiguity", () => {
            var f = new Fixture(); f.Registry.Units.Add(f.Unit);
            Has(f.Issues(), "REFERENCE_DUPLICATE"); Lacks(f.Issues(), "UNIT_BASE_IDENTITY_AMBIGUOUS");
        });
        Run("unused registered job is validated", () => {
            var f = new Fixture(); f.Registry.Jobs.Add(new Train { Id = "unused" }); Has(f.Issues(), "REFERENCE_REQUIRED");
        });
        Run("invalid then repaired, context does not leak", () => {
            var f = new Fixture(); f.Registry.Abilities.Clear(); Has(f.Issues(), "BASE_ABILITY_NOT_IN_REGISTRY");
            f.Registry.Abilities.Add(f.Gather); Clean(f.Registry);
        });
        Run("registration order does not change diagnostic set", () => {
            var f = new Fixture(); f.AddTech("other", "research2"); f.Research.WorkLoad = -1;
            var before = Codes(f.Issues()); f.Registry.Jobs.Reverse(); f.Registry.Civs.Reverse(); f.Tree.Nodes.Reverse();
            Equal(before, Codes(f.Issues()));
        });
        Run("null entries and owned lists do not throw", () => {
            var f = new Fixture(); f.Registry.Jobs.Add(null); f.Registry.Civs.Add(null); f.Tree.Nodes.Add(null);
            f.Base.Abilities = null; f.Base.Jobs = null; f.Civ.UnitUnlocks.Add(null);
            Has(f.Issues(), "LIST_NULL"); Has(f.Issues(), "TREE_NODE_NULL"); Has(f.Issues(), "CIV_UNLOCK_ENTRY_NULL");
        });
        Run("Tech invalid cost still validated", () => {
            var f = new Fixture(); f.Tech.Cost.Add(new ResourcePair(ResourceType.Gold, float.NaN)); Has(f.Issues(), "NUMBER_RANGE");
            f.Tech.Cost = null; Has(f.Issues(), "LIST_NULL");
        });
        Run("cost overflow still detected", () => {
            var f = new Fixture(); f.Tech.Cost.Add(new ResourcePair(ResourceType.Gold, float.MaxValue));
            f.Tech.Cost.Add(new ResourcePair(ResourceType.Gold, float.MaxValue)); Has(f.Issues(), "COST_TOTAL_OVERFLOW");
        });
        Run("Tech missing and whitespace IDs", () => {
            var f = new Fixture(); f.Tech.IDs = default;
            var issue = Has(f.Issues(), "ID_REQUIRED"); Equal(f.Tech, issue.Asset); Equal("IDs", issue.FieldPath);
            f.Tech.IDs = new Unity.Collections.FixedString64Bytes("  "); Has(f.Issues(), "ID_REQUIRED");
        });
        Run("Tech duplicate IDs with distinct Research IDs", () => {
            var f = new Fixture(); var other = f.AddTech("other", "research2"); other.IDs = f.Tech.IDs;
            var before = Snapshot(f.Registry); var issue = Has(f.Issues(), "ID_DUPLICATE");
            Equal(other, issue.Asset); Equal(f.Tech, issue.RelatedAsset); Equal("IDs", issue.FieldPath);
            Equal(before, Snapshot(f.Registry));
        });
        Run("Tech and Research ID scopes remain separate", () => {
            var f = new Fixture(); f.Tech.IDs = new Unity.Collections.FixedString64Bytes(f.Research.Id); Clean(f.Registry);
        });
        Run("Build valid empty and cyclic references are read-only", () => {
            var f = new Fixture(); var b = AddBuild(f); Clean(f.Registry);
            b.Buildings.Add(f.Registry.Buildings[0]); f.Registry.Buildings[0].basedSO.Abilities.Add(b);
            var before = Snapshot(f.Registry); Clean(f.Registry); Equal(before, Snapshot(f.Registry));
        });
        Run("Build null list and null entry", () => {
            var f = new Fixture(); var b = AddBuild(f); b.Buildings = null;
            var issue = Has(f.Issues(), "LIST_NULL"); Equal(b, issue.Asset); Equal("Buildings", issue.FieldPath);
            b.Buildings = new List<BuildingSO> { null }; issue = Has(f.Issues(), "REFERENCE_REQUIRED");
            Equal(b, issue.Asset); Equal("Buildings[0]", issue.FieldPath);
        });
        Run("Build outside reference does not register or traverse", () => {
            var f = new Fixture(); var b = AddBuild(f);
            var outside = new BuildingSO { basedSO = new BasedSO { ID = f.Registry.Buildings[0].basedSO.ID } };
            b.Buildings.Add(outside); var before = Snapshot(f.Registry); var issues = f.Issues();
            var issue = Has(issues, "BUILD_BUILDING_NOT_IN_REGISTRY");
            Equal(b, issue.Asset); Equal(outside, issue.RelatedAsset); Equal("Buildings[0]", issue.FieldPath);
            Assert(!issues.Any(x => x.Asset == outside || x.Asset == outside.basedSO), "outside building traversed");
            Equal(before, Snapshot(f.Registry));
        });
        Run("Build duplicate building", () => {
            var f = new Fixture(); var b = AddBuild(f); b.Buildings.Add(f.Registry.Buildings[0]); b.Buildings.Add(f.Registry.Buildings[0]);
            Equal("Buildings[1]", Has(f.Issues(), "REFERENCE_DUPLICATE").FieldPath);
        });
        Run("Build building needs Base", () => {
            var f = new Fixture(); var b = AddBuild(f); var building = f.Registry.Buildings[0];
            b.Buildings.Add(building); building.basedSO = null;
            var issue = Has(f.Issues(), "REFERENCE_REQUIRED"); Equal(building, issue.Asset); Equal("basedSO", issue.FieldPath);
        });
        Console.WriteLine($"PASS: {count} standalone validation cases (not Unity integration tests).");
        return 0;
    }

    static BuildAbilityDefinition AddBuild(Fixture f)
    {
        var b = new BuildAbilityDefinition { Id = "build" }; f.Registry.Abilities.Add(b); f.Base.Abilities.Add(b); return b;
    }
    static void Run(string name, Action test) { test(); count++; Console.WriteLine("PASS " + name); }
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    static void Equal<T>(T expected, T actual) => Assert(EqualityComparer<T>.Default.Equals(expected, actual), $"Expected {expected}, got {actual}");
    static ValidationIssue Has(IReadOnlyList<ValidationIssue> issues, string code)
    {
        var issue = issues.FirstOrDefault(x => x.Code == code);
        Assert(issue != null, "Missing " + code + "; got " + Codes(issues)); return issue;
    }
    static void Lacks(IReadOnlyList<ValidationIssue> issues, string code) => Assert(!issues.Any(x => x.Code == code), "Unexpected " + code);
    static string Codes(IReadOnlyList<ValidationIssue> issues) => string.Join(",", issues.Select(x => x.Code).OrderBy(x => x));
    static void Clean(GameDataRegistry registry)
    {
        var issues = GameDataValidator.Validate(registry);
        Assert(!issues.Any(x => x.Severity == ValidationSeverity.Error), "Unexpected errors: " + Codes(issues));
    }
    static void SetTech(Research r, TechDefinition t) => typeof(Research).GetField("techDefinition", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(r, t);

    // Snapshot all reachable managed fields, including private serialized references and cycles.
    static string Snapshot(object root)
    {
        var seen = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
        string Read(object value)
        {
            if (value == null) return "null";
            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is string) return type.Name + ":" + value;
            if (!type.IsValueType)
            {
                if (seen.TryGetValue(value, out int id)) return "ref:" + id;
                seen.Add(value, seen.Count);
            }
            if (value is IList list) return "[" + string.Join(";", list.Cast<object>().Select(Read)) + "]";
            return type.Name + "{" + string.Join(";", type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .OrderBy(x => x.Name).Select(x => x.Name + "=" + Read(x.GetValue(value)))) + "}";
        }
        return Read(root);
    }

    sealed class Fixture
    {
        public GameDataRegistry Registry = new();
        public BasedSO Base = new() { ID = "worker", Prefab = new GameObject(), name = "worker base" };
        public UnitSO Unit;
        public GatherAbilityDefinition Gather = new() { Id = "gather" };
        public Train Train = new() { Id = "train", name = "train" };
        public Research Research = new() { Id = "research", name = "research" };
        public TechDefinition Tech = new() { name = "tech", IDs = new Unity.Collections.FixedString64Bytes("tech") };
        public TechTreeDef Tree = new() { Id = "tree" };
        public CivDef Civ = new() { Id = "civ" };
        public Fixture()
        {
            Unit = new UnitSO { basedSO = Base }; Train.OutputUnit = Unit; SetTech(Research, Tech);
            Base.Abilities.Add(Gather); Base.Jobs.Add(Train);
            var producer = new BasedSO { ID = "producer", Prefab = new GameObject(), Jobs = new List<Job> { Research, Train } };
            Registry.Units.Add(Unit); Registry.Buildings.Add(new BuildingSO { basedSO = producer });
            Registry.Abilities.Add(Gather); Registry.Jobs.Add(Train); Registry.Jobs.Add(Research);
            Tree.Nodes.Add(new TechTreeNode { techDefinition = Tech }); Civ.TechTree = Tree;
            Civ.UnitUnlocks.Add(new CivUnitUnlockEntry { unitDefinition = Unit, Prerequisites = new List<TechDefinition> { Tech } });
            Registry.Civs.Add(Civ);
        }
        public TechDefinition AddTech(string name, string researchId)
        {
            var tech = new TechDefinition { name = name, IDs = new Unity.Collections.FixedString64Bytes(name) };
            var research = new Research { Id = researchId }; SetTech(research, tech);
            Tree.Nodes.Add(new TechTreeNode { techDefinition = tech }); Registry.Jobs.Add(research); return tech;
        }
        public IReadOnlyList<ValidationIssue> Issues() => GameDataValidator.Validate(Registry);
    }
}
