namespace VcvPatchDiagram.Core.Analysis;

public static class GraphOrder
{
    /// <summary>
    /// Kahn topological sort, stable on the input order. Feedback loops (frequent in patches) are broken with the
    /// greedy rule of Eades, Lin &amp; Smyth (1993), the usual first step of a Sugiyama layout: when no node is free,
    /// release the one with the most outgoing minus incoming edges among the remaining ones (earliest on a tie),
    /// which keeps the cables that end up going backwards few. Every node is returned exactly once.
    /// </summary>
    public static List<long> Topological(IReadOnlyList<long> nodes, IReadOnlyList<(long From, long To)> edges)
    {
        Dictionary<long, int> inDegree = nodes.ToDictionary(n => n, n => 0);
        foreach ((long from, long to) in edges)
        {
            if (inDegree.ContainsKey(from) && inDegree.ContainsKey(to) && from != to)
            {
                inDegree[to]++;
            }
        }

        List<long> remaining = nodes.ToList();
        List<long> result = new List<long>();
        while (remaining.Count > 0)
        {
            long next = remaining.FirstOrDefault(n => inDegree[n] == 0, Greediest(remaining, edges));
            remaining.Remove(next);
            result.Add(next);
            foreach ((long from, long to) in edges)
            {
                if (from == next && from != to && remaining.Contains(to))
                {
                    inDegree[to]--;
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Feedback loops: groups of nodes that can all reach each other (strongly connected components of two or more
    /// nodes, Tarjan 1972), in the input order.
    /// </summary>
    public static List<List<long>> Loops(IReadOnlyList<long> nodes, IReadOnlyList<(long From, long To)> edges)
    {
        Dictionary<long, List<long>> next = nodes.ToDictionary(n => n, n => edges.Where(e => e.From == n && e.To != n).Select(e => e.To).Distinct().ToList());
        Dictionary<long, int> index = new Dictionary<long, int>();
        Dictionary<long, int> low = new Dictionary<long, int>();
        Stack<long> stack = new Stack<long>();
        HashSet<long> onStack = new HashSet<long>();
        List<List<long>> loops = new List<List<long>>();

        void Visit(long node)
        {
            index[node] = low[node] = index.Count;
            stack.Push(node);
            onStack.Add(node);
            foreach (long to in next[node].Where(next.ContainsKey))
            {
                if (!index.ContainsKey(to))
                {
                    Visit(to);
                    low[node] = Math.Min(low[node], low[to]);
                }
                else if (onStack.Contains(to))
                {
                    low[node] = Math.Min(low[node], index[to]);
                }
            }
            if (low[node] != index[node])
            {
                return;
            }
            List<long> component = new List<long>();
            long member;
            do
            {
                member = stack.Pop();
                onStack.Remove(member);
                component.Add(member);
            }
            while (member != node);
            if (component.Count > 1)
            {
                loops.Add(nodes.Where(component.Contains).ToList());
            }
        }

        foreach (long node in nodes.Where(n => !index.ContainsKey(n)))
        {
            Visit(node);
        }
        return loops.OrderBy(l => nodes.ToList().IndexOf(l[0])).ToList();
    }

    private static long Greediest(List<long> remaining, IReadOnlyList<(long From, long To)> edges)
    {
        HashSet<long> left = remaining.ToHashSet();
        int Balance(long n) => edges.Count(e => e.From == n && e.To != n && left.Contains(e.To)) - edges.Count(e => e.To == n && e.From != n && left.Contains(e.From));
        return remaining.Select((n, i) => (n, i)).OrderByDescending(p => Balance(p.n)).ThenBy(p => p.i).First().n;
    }

    /// <summary>Longest-path depth of each node (sources at 0), loops broken the same way as <see cref="Topological"/>.</summary>
    public static Dictionary<long, int> Depths(IReadOnlyList<long> nodes, IReadOnlyList<(long From, long To)> edges)
    {
        List<long> order = Topological(nodes, edges);
        Dictionary<long, int> position = order.Select((n, i) => (n, i)).ToDictionary(p => p.n, p => p.i);
        Dictionary<long, int> depth = nodes.ToDictionary(n => n, n => 0);
        foreach (long node in order)
        {
            foreach ((long from, long to) in edges)
            {
                // Only forward edges in the chosen order count, so loops can't grow depths forever.
                if (from == node && position.TryGetValue(to, out int toPos) && toPos > position[node])
                {
                    depth[to] = Math.Max(depth[to], depth[node] + 1);
                }
            }
        }
        return depth;
    }
}