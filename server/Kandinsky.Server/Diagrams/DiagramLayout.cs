using Kandinsky.Server.Model;

namespace Kandinsky.Server.Diagrams;

/// <summary>
/// Layered layout: connected shapes in rows following the edge direction, unconnected shapes in a grid below.
/// The content of a group (shapes with that group as ParentId) is laid out the same way inside the group, which is sized to fit it.
/// </summary>
public static class DiagramLayout
{
    private const double Origin = 40;
    private const double ColumnGap = 60;
    private const double RowGap = 80;
    private const int GridColumns = 5;
    private const int OrderingSweeps = 8;
    private const double GroupPadding = 20;
    private const double GroupTitleHeight = 36;

    public static IReadOnlyList<Shape> Arrange(IReadOnlyList<Shape> shapes, IReadOnlyList<Edge> edges)
    {
        var byId = shapes.ToDictionary(s => s.Id);
        var parent = shapes.ToDictionary(s => s.Id, s => s.ParentId is { } p && byId.TryGetValue(p, out var g) && g.Kind == ShapeKind.Group ? p : null);
        BreakCycles(parent);
        var children = shapes.Where(s => parent[s.Id] is not null).ToLookup(s => parent[s.Id]!);
        var sizes = shapes.ToDictionary(s => s.Id, s => (s.Width, s.Height));
        var offsets = new Dictionary<string, (double X, double Y)>();

        // The child of the container that is the shape itself or contains it, if any.
        string? ChildOf(string? container, string id)
        {
            for (string? current = id; current is not null; current = parent[current])
            {
                if (parent[current] == container)
                {
                    return current;
                }
            }
            return null;
        }

        // Lays out the content of a container (null for the top level) relative to its content origin and returns its size.
        (double Width, double Height) Layout(string? container, IReadOnlyList<Shape> content)
        {
            foreach (var child in content.Where(c => children[c.Id].Any()))
            {
                var (width, height) = Layout(child.Id, children[child.Id].ToList());
                sizes[child.Id] = (width + 2 * GroupPadding, height + GroupTitleHeight + GroupPadding);
            }
            var index = content.Select((s, i) => (s.Id, i)).ToDictionary(p => p.Id, p => p.i);
            var links = edges
                .Select(e => (From: ChildOf(container, e.SourceId), To: ChildOf(container, e.TargetId)))
                .Where(l => l.From is not null && l.To is not null && l.From != l.To)
                .Select(l => (index[l.From!], index[l.To!]))
                .ToList();
            var positions = ArrangeLevel(content.Select(s => sizes[s.Id]).ToList(), links);
            for (var i = 0; i < content.Count; i++)
            {
                offsets[content[i].Id] = positions[i];
            }
            return content.Count == 0
                ? (0, 0)
                : (content.Select((s, i) => positions[i].X + sizes[s.Id].Width).Max(), content.Select((s, i) => positions[i].Y + sizes[s.Id].Height).Max());
        }

        Layout(null, shapes.Where(s => parent[s.Id] is null).ToList());

        var placed = new Dictionary<string, Shape>();
        Shape Place(Shape s)
        {
            if (placed.TryGetValue(s.Id, out var done))
            {
                return done;
            }
            var (x, y) = offsets[s.Id];
            if (parent[s.Id] is { } p)
            {
                var group = Place(byId[p]);
                (x, y) = (group.X + GroupPadding + x, group.Y + GroupTitleHeight + y);
            }
            else
            {
                (x, y) = (Origin + x, Origin + y);
            }
            return placed[s.Id] = s with { X = Math.Round(x), Y = Math.Round(y), Width = sizes[s.Id].Width, Height = sizes[s.Id].Height };
        }
        return shapes.Select(Place).ToList();
    }

    /// <summary>Moves shapes whose parent chain loops back to them to the top level.</summary>
    private static void BreakCycles(Dictionary<string, string?> parent)
    {
        foreach (var id in parent.Keys.ToList())
        {
            var seen = new HashSet<string> { id };
            for (var current = parent[id]; current is not null; current = parent[current])
            {
                if (!seen.Add(current))
                {
                    parent[id] = null;
                    break;
                }
            }
        }
    }

    /// <summary>Arranges boxes of the given sizes with the top-left of the whole arrangement at (0, 0).</summary>
    private static (double X, double Y)[] ArrangeLevel(IReadOnlyList<(double Width, double Height)> shapes, List<(int From, int To)> links)
    {
        links = links.Distinct().ToList();
        var connected = links.SelectMany(l => new[] { l.From, l.To }).ToHashSet();

        var acyclic = ReverseBackEdges(shapes.Count, links);
        var layers = OrderLayers(AssignLayers(shapes.Count, acyclic, connected), acyclic);

        var positions = new (double X, double Y)[shapes.Count];
        var bottom = PlaceLayers(shapes, layers, positions);
        PlaceGrid(shapes, Enumerable.Range(0, shapes.Count).Where(i => !connected.Contains(i)).ToList(), bottom, positions);
        return positions;
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

    private static double PlaceLayers(IReadOnlyList<(double Width, double Height)> shapes, List<List<int>> layers, (double X, double Y)[] positions)
    {
        var rowWidths = layers.Select(row => row.Sum(v => shapes[v].Width) + ColumnGap * (row.Count - 1)).ToList();
        var maxWidth = rowWidths.DefaultIfEmpty(0).Max();
        var y = 0.0;

        for (var i = 0; i < layers.Count; i++)
        {
            var x = (maxWidth - rowWidths[i]) / 2;
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

    private static void PlaceGrid(IReadOnlyList<(double Width, double Height)> shapes, List<int> items, double y, (double X, double Y)[] positions)
    {
        foreach (var row in items.Chunk(GridColumns))
        {
            var x = 0.0;
            foreach (var v in row)
            {
                positions[v] = (x, y);
                x += shapes[v].Width + ColumnGap;
            }
            y += row.Max(v => shapes[v].Height) + RowGap;
        }
    }
}
