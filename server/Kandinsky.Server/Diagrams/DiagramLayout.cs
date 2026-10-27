using Kandinsky.Server.Model;

namespace Kandinsky.Server.Diagrams;

/// <summary>Layered layout: connected shapes in rows following the edge direction, unconnected shapes in a grid below.</summary>
public static class DiagramLayout
{
    private const double Origin = 40;
    private const double ColumnGap = 60;
    private const double RowGap = 80;
    private const int GridColumns = 5;
    private const int OrderingSweeps = 8;

    public static IReadOnlyList<Shape> Arrange(IReadOnlyList<Shape> shapes, IReadOnlyList<Edge> edges)
    {
        var index = shapes.Select((s, i) => (s.Id, i)).ToDictionary(p => p.Id, p => p.i);
        var links = edges
            .Where(e => e.SourceId != e.TargetId && index.ContainsKey(e.SourceId) && index.ContainsKey(e.TargetId))
            .Select(e => (From: index[e.SourceId], To: index[e.TargetId]))
            .Distinct()
            .ToList();
        var connected = links.SelectMany(l => new[] { l.From, l.To }).ToHashSet();

        var acyclic = ReverseBackEdges(shapes.Count, links);
        var layers = OrderLayers(AssignLayers(shapes.Count, acyclic, connected), acyclic);

        var positions = new (double X, double Y)[shapes.Count];
        var bottom = PlaceLayers(shapes, layers, positions);
        PlaceGrid(shapes, Enumerable.Range(0, shapes.Count).Where(i => !connected.Contains(i)).ToList(), bottom, positions);

        return shapes.Select((s, i) => s with { X = Math.Round(positions[i].X), Y = Math.Round(positions[i].Y) }).ToList();
    }

    private static List<(int From, int To)> ReverseBackEdges(int count, List<(int From, int To)> links)
    {
        var outgoing = links.ToLookup(l => l.From, l => l.To);
        var hasIncoming = links.Select(l => l.To).ToHashSet();
        var state = new int[count]; // 0 = unvisited, 1 = on the DFS stack, 2 = done
        var back = new HashSet<(int, int)>();

        void Visit(int v)
        {
            state[v] = 1;
            foreach (var w in outgoing[v])
            {
                if (state[w] == 1)
                {
                    back.Add((v, w));
                }
                else if (state[w] == 0)
                {
                    Visit(w);
                }
            }
            state[v] = 2;
        }

        // Start from sources so they end up in the top layer.
        foreach (var v in Enumerable.Range(0, count).OrderBy(v => hasIncoming.Contains(v)))
        {
            if (state[v] == 0)
            {
                Visit(v);
            }
        }

        return links.Select(l => back.Contains(l) ? (l.To, l.From) : l).Distinct().ToList();
    }

    private static List<List<int>> AssignLayers(int count, List<(int From, int To)> links, HashSet<int> connected)
    {
        var layer = new int[count];
        var indegree = new int[count];
        foreach (var link in links)
        {
            indegree[link.To]++;
        }

        var outgoing = links.ToLookup(l => l.From, l => l.To);
        var queue = new Queue<int>(Enumerable.Range(0, count).Where(v => indegree[v] == 0));
        while (queue.Count > 0)
        {
            var v = queue.Dequeue();
            foreach (var w in outgoing[v])
            {
                layer[w] = Math.Max(layer[w], layer[v] + 1);
                if (--indegree[w] == 0)
                {
                    queue.Enqueue(w);
                }
            }
        }

        return connected
            .Order()
            .GroupBy(v => layer[v])
            .OrderBy(g => g.Key)
            .Select(g => g.ToList())
            .ToList();
    }

    /// <summary>Barycenter heuristic: alternately sort each layer by the mean position of its neighbours in the adjacent layer.</summary>
    private static List<List<int>> OrderLayers(List<List<int>> layers, List<(int From, int To)> links)
    {
        var neighbours = links.SelectMany(l => new[] { (l.From, l.To), (l.To, l.From) }).ToLookup(p => p.Item1, p => p.Item2);

        for (var sweep = 0; sweep < OrderingSweeps; sweep++)
        {
            var down = sweep % 2 == 0;
            var sequence = down ? Enumerable.Range(1, Math.Max(0, layers.Count - 1)) : Enumerable.Range(0, Math.Max(0, layers.Count - 1)).Reverse();
            foreach (var i in sequence)
            {
                var reference = layers[down ? i - 1 : i + 1]
                    .Select((v, position) => (v, position))
                    .ToDictionary(p => p.v, p => (double)p.position);
                layers[i] = layers[i]
                    .Select((v, position) =>
                    {
                        var adjacent = neighbours[v].Where(reference.ContainsKey).Select(n => reference[n]).ToList();
                        return (v, key: adjacent.Count > 0 ? adjacent.Average() : position);
                    })
                    .OrderBy(p => p.key)
                    .Select(p => p.v)
                    .ToList();
            }
        }
        return layers;
    }

    private static double PlaceLayers(IReadOnlyList<Shape> shapes, List<List<int>> layers, (double X, double Y)[] positions)
    {
        var rowWidths = layers.Select(row => row.Sum(v => shapes[v].Width) + ColumnGap * (row.Count - 1)).ToList();
        var maxWidth = rowWidths.DefaultIfEmpty(0).Max();
        var y = Origin;

        for (var i = 0; i < layers.Count; i++)
        {
            var x = Origin + (maxWidth - rowWidths[i]) / 2;
            var rowHeight = layers[i].Max(v => shapes[v].Height);
            foreach (var v in layers[i])
            {
                positions[v] = (x, y + (rowHeight - shapes[v].Height) / 2);
                x += shapes[v].Width + ColumnGap;
            }
            y += rowHeight + RowGap;
        }
        return y;
    }

    private static void PlaceGrid(IReadOnlyList<Shape> shapes, List<int> items, double y, (double X, double Y)[] positions)
    {
        foreach (var row in items.Chunk(GridColumns))
        {
            var x = Origin;
            foreach (var v in row)
            {
                positions[v] = (x, y);
                x += shapes[v].Width + ColumnGap;
            }
            y += row.Max(v => shapes[v].Height) + RowGap;
        }
    }
}
