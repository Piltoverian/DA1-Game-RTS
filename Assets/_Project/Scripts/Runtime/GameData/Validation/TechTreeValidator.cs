using System.Collections.Generic;

namespace RTS.DataValidation
{
    internal static class TechTreeValidator
    {
        private readonly struct Edge
        {
            internal readonly TechDefinition Target;
            internal readonly string Field;
            internal Edge(TechDefinition target, string field) { Target = target; Field = field; }
        }

        internal static void Validate(TechTreeDef tree, ValidationContext c)
        {
            if (!c.List(tree.Nodes, tree, nameof(tree.Nodes))) return;
            var members = new HashSet<TechDefinition>();
            var order = new List<TechDefinition>();
            var edges = new Dictionary<TechDefinition, List<Edge>>();
            for (int i = 0; i < tree.Nodes.Count; i++)
            {
                var node = tree.Nodes[i];
                string path = $"Nodes[{i}]";
                if (node == null)
                {
                    c.Error(tree, path, "TREE_NODE_NULL", "Node must not be null.");
                    continue;
                }
                if (!ValidationContext.Finite(node.Position.x) || !ValidationContext.Finite(node.Position.y))
                    c.Error(tree, path + ".Position", "NUMBER_RANGE", "Node position must be finite.");
                if (!c.Required(node.techDefinition, tree, path + ".techDefinition")) continue;
                c.Visit(node.techDefinition);
                c.RequireResearch(node.techDefinition, tree, path + ".techDefinition");
                if (!members.Add(node.techDefinition))
                    c.Error(tree, path + ".techDefinition", "TREE_TECH_DUPLICATE", "Tech appears more than once in this tree.", node.techDefinition);
                else
                {
                    order.Add(node.techDefinition);
                    edges.Add(node.techDefinition, new List<Edge>());
                }
            }

            for (int i = 0; i < tree.Nodes.Count; i++)
            {
                var node = tree.Nodes[i];
                if (node == null) continue;
                var path = $"Nodes[{i}].Prerequisites";
                if (!c.List(node.Prerequisites, tree, path)) continue;
                var seen = new HashSet<TechDefinition>();
                for (int j = 0; j < node.Prerequisites.Count; j++)
                {
                    var prerequisite = node.Prerequisites[j];
                    var field = $"{path}[{j}]";
                    if (!c.Required(prerequisite, tree, field)) continue;
                    if (!seen.Add(prerequisite))
                    {
                        c.Error(tree, field, "TREE_PREREQUISITE_DUPLICATE", "Prerequisite appears more than once on this node.", prerequisite);
                        continue;
                    }
                    if (!members.Contains(prerequisite))
                    {
                        c.Error(tree, field, "TREE_PREREQUISITE_OUTSIDE_TREE", "Prerequisite must belong to this tree.", prerequisite);
                        continue;
                    }
                    c.Visit(prerequisite);
                    if (prerequisite == node.techDefinition)
                    {
                        c.Error(tree, field, "TREE_SELF_REFERENCE", "Tech cannot require itself.", prerequisite);
                        continue;
                    }
                    if (node.techDefinition != null) edges[node.techDefinition].Add(new Edge(prerequisite, field));
                }
            }
            FindCycles(tree, order, edges, c);
        }

        // Iterative DFS handles deep trees without consuming the C# call stack.
        // All components are visited; disconnected groups and multiple roots are valid.
        private static void FindCycles(TechTreeDef tree, List<TechDefinition> order,
            Dictionary<TechDefinition, List<Edge>> edges, ValidationContext c)
        {
            var done = new HashSet<TechDefinition>();
            var active = new Dictionary<TechDefinition, int>();
            var path = new List<TechDefinition>();
            var nextEdges = new List<int>();
            foreach (var root in order)
            {
                if (done.Contains(root)) continue;
                path.Add(root);
                nextEdges.Add(0);
                active.Add(root, 0);
                while (path.Count > 0)
                {
                    int top = path.Count - 1;
                    var current = path[top];
                    int next = nextEdges[top];
                    if (next == edges[current].Count)
                    {
                        done.Add(current);
                        active.Remove(current);
                        path.RemoveAt(top);
                        nextEdges.RemoveAt(top);
                        continue;
                    }
                    var edge = edges[current][next];
                    nextEdges[top]++;
                    if (active.TryGetValue(edge.Target, out int start))
                    {
                        var names = new List<string>();
                        for (int k = start; k < path.Count; k++) names.Add(Label(path[k]));
                        names.Add(Label(edge.Target));
                        c.Error(tree, edge.Field, "TREE_CYCLE", "Prerequisite cycle: " + string.Join(" -> ", names), edge.Target);
                    }
                    else if (!done.Contains(edge.Target))
                    {
                        active.Add(edge.Target, path.Count);
                        path.Add(edge.Target);
                        nextEdges.Add(0);
                    }
                }
            }
        }

        private static string Label(TechDefinition tech) => tech.name;
    }
}
