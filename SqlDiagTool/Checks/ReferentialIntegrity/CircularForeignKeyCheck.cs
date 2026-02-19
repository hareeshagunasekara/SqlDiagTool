using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Data.SqlClient;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Checks;

// Detects cycles in the FK graph 
public sealed class CircularForeignKeyCheck : IStructureCheck
{
    public int Id => 25;
    public string Name => "Circular FK Dependencies";
    public string Category => "Referential Integrity";
    public string Code => "CIRCULAR_FK";

    public async Task<TestResult> RunAsync(string connectionString)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var rows = await SqlHelper.RunQueryAsync(connectionString, ReferentialIntegrityQueries.FkEdges);
            sw.Stop();

            var edges = new List<(int From, int To)>();
            var idToName = new Dictionary<int, string>();
            foreach (var r in rows)
            {
                if (r.Length < 6 || !int.TryParse(r[0], out var parentId) || !int.TryParse(r[1], out var refId)) continue;
                edges.Add((parentId, refId));
                idToName[parentId] = $"{r[2]}.{r[3]}";
                idToName[refId] = $"{r[4]}.{r[5]}";
            }

            var nodesInCycle = new HashSet<int>();
            var adj = edges.GroupBy(e => e.From).ToDictionary(g => g.Key, g => g.Select(e => e.To).ToList());

            foreach (var start in adj.Keys.Where(n => !nodesInCycle.Contains(n)))
                FindCycles(start, adj, new List<int>(), new HashSet<int>(), nodesInCycle);

            var items = nodesInCycle.Where(id => idToName.TryGetValue(id, out _)).Select(id => idToName[id]).OrderBy(x => x).ToList();
            if (items.Count == 0)
                return new TestResult(Name, Status.PASS, "No circular FK dependencies", sw.ElapsedMilliseconds, Id, Category, Code);
            var details = string.Join("; ", items.Take(10));
            var more = items.Count > 10 ? $" ... and {items.Count - 10} more" : "";
            return new TestResult(Name, Status.WARNING, $"Circular FK dependency detected: {details}{more}", sw.ElapsedMilliseconds, Id, Category, Code, items);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult(Name, Status.FAIL, $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds, Id, Category, Code);
        }
    }

    private static void FindCycles(int node, IReadOnlyDictionary<int, List<int>> adj, List<int> path, HashSet<int> pathSet, HashSet<int> nodesInCycle)
    {
        if (pathSet.Contains(node))
        {
            var start = path.IndexOf(node);
            for (var i = start; i < path.Count; i++) nodesInCycle.Add(path[i]);
            return;
        }
        if (nodesInCycle.Contains(node)) return;

        path.Add(node);
        pathSet.Add(node);
        if (adj.TryGetValue(node, out var neighbors))
            foreach (var next in neighbors) FindCycles(next, adj, path, pathSet, nodesInCycle);
        path.RemoveAt(path.Count - 1);
        pathSet.Remove(node);
    }
}
