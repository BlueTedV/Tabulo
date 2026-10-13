namespace Tabulo.Core.Engine;

/// <summary>
/// Directed Acyclic Graph (DAG) for tracking formula cell dependencies and computing topological recalculation order.
/// </summary>
public class DependencyGraph
{
    // Key depends on Values (Key -> Dependencies)
    private readonly Dictionary<QualifiedCellAddress, HashSet<QualifiedCellAddress>> _dependencies = new();

    // Values depend on Key (Key -> Dependents)
    private readonly Dictionary<QualifiedCellAddress, HashSet<QualifiedCellAddress>> _dependents = new();

    public void SetDependencies(QualifiedCellAddress cell, IEnumerable<QualifiedCellAddress> directDependencies)
    {
        // 1. Clear old dependencies
        if (_dependencies.TryGetValue(cell, out var oldDeps))
        {
            foreach (var oldDep in oldDeps)
            {
                if (_dependents.TryGetValue(oldDep, out var oldDownstream))
                {
                    oldDownstream.Remove(cell);
                    if (oldDownstream.Count == 0) _dependents.Remove(oldDep);
                }
            }
        }

        var newDepSet = new HashSet<QualifiedCellAddress>(directDependencies);
        if (newDepSet.Count > 0)
        {
            _dependencies[cell] = newDepSet;

            // Register downstream dependents
            foreach (var dep in newDepSet)
            {
                if (!_dependents.TryGetValue(dep, out var downstream))
                {
                    downstream = new HashSet<QualifiedCellAddress>();
                    _dependents[dep] = downstream;
                }
                downstream.Add(cell);
            }
        }
        else
        {
            _dependencies.Remove(cell);
        }
    }

    public void RemoveCell(QualifiedCellAddress cell)
    {
        SetDependencies(cell, Enumerable.Empty<QualifiedCellAddress>());
        _dependents.Remove(cell);
    }

    public bool HasCircularDependency(QualifiedCellAddress startCell, out List<QualifiedCellAddress> cycle)
    {
        var visited = new HashSet<QualifiedCellAddress>();
        var recursionStack = new HashSet<QualifiedCellAddress>();
        var path = new List<QualifiedCellAddress>();

        bool Dfs(QualifiedCellAddress current)
        {
            visited.Add(current);
            recursionStack.Add(current);
            path.Add(current);

            if (_dependencies.TryGetValue(current, out var deps))
            {
                foreach (var dep in deps)
                {
                    if (!visited.Contains(dep))
                    {
                        if (Dfs(dep)) return true;
                    }
                    else if (recursionStack.Contains(dep))
                    {
                        path.Add(dep);
                        return true;
                    }
                }
            }

            recursionStack.Remove(current);
            path.RemoveAt(path.Count - 1);
            return false;
        }

        if (Dfs(startCell))
        {
            cycle = path;
            return true;
        }

        cycle = new List<QualifiedCellAddress>();
        return false;
    }

    /// <summary>
    /// Computes the exact topological recalculation order for all downstream dependents of the changed cells.
    /// </summary>
    public List<QualifiedCellAddress> GetEvaluationOrder(IEnumerable<QualifiedCellAddress> changedRoots, out HashSet<QualifiedCellAddress> circularCells)
    {
        circularCells = new HashSet<QualifiedCellAddress>();

        // 1. Collect all reachable downstream nodes
        var allAffected = new HashSet<QualifiedCellAddress>();
        var queue = new Queue<QualifiedCellAddress>(changedRoots);

        while (queue.Count > 0)
        {
            var curr = queue.Dequeue();
            if (_dependents.TryGetValue(curr, out var downstreams))
            {
                foreach (var downstream in downstreams)
                {
                    if (allAffected.Add(downstream))
                    {
                        queue.Enqueue(downstream);
                    }
                }
            }
        }

        if (allAffected.Count == 0)
        {
            return new List<QualifiedCellAddress>();
        }

        // 2. Compute in-degree within the affected subgraph
        var inDegree = new Dictionary<QualifiedCellAddress, int>();
        foreach (var node in allAffected)
        {
            var count = 0;
            if (_dependencies.TryGetValue(node, out var deps))
            {
                foreach (var dep in deps)
                {
                    if (allAffected.Contains(dep))
                    {
                        count++;
                    }
                }
            }
            inDegree[node] = count;
        }

        // 3. Kahn's Algorithm for topological sort
        var readyQueue = new Queue<QualifiedCellAddress>(allAffected.Where(n => inDegree[n] == 0));
        var order = new List<QualifiedCellAddress>();

        while (readyQueue.Count > 0)
        {
            var node = readyQueue.Dequeue();
            order.Add(node);

            if (_dependents.TryGetValue(node, out var downstreams))
            {
                foreach (var downstream in downstreams)
                {
                    if (allAffected.Contains(downstream))
                    {
                        inDegree[downstream]--;
                        if (inDegree[downstream] == 0)
                        {
                            readyQueue.Enqueue(downstream);
                        }
                    }
                }
            }
        }

        // 4. Any affected node not included in order is part of a cycle
        if (order.Count < allAffected.Count)
        {
            foreach (var node in allAffected)
            {
                if (!order.Contains(node))
                {
                    circularCells.Add(node);
                }
            }
        }

        return order;
    }
}
