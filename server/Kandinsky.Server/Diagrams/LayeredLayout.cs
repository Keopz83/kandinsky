namespace Kandinsky.Server.Diagrams;

/// <summary>
/// Layered (Sugiyama-style) layout of boxes: connected boxes in rows following the link direction, unconnected boxes in a grid below.
/// Boxes and links are given by index; positions are relative to the top-left of the whole arrangement at (0, 0).
/// </summary>
internal static class LayeredLayout
{
    private const double ColumnGap = 60;
    private const double RowGap = 80;
    private const int GridColumns = 5;
    private const int OrderingSweeps = 8;

    private enum VisitState
    {
        Unvisited,
        OnStack,
        Done,
    }

    public static (double X, double Y)[] Arrange(IReadOnlyList<(double Width, double Height)> boxes, List<(int From, int To)> links)
    {
        links = links.Distinct().ToList();
        var connected = links.SelectMany(l => new[] { l.From, l.To }).ToHashSet();

        var acyclicLinks = ReverseBackLinks(boxes.Count, links);
        var layers = OrderLayers(AssignLayers(boxes.Count, acyclicLinks, connected), acyclicLinks);

        var positions = new (double X, double Y)[boxes.Count];
        var bottom = PlaceLayers(boxes, layers, positions);
        var unconnected = Enumerable.Range(0, boxes.Count).Where(i => !connected.Contains(i)).ToList();
        PlaceGrid(boxes, unconnected, bottom, positions);
        return positions;
    }

    /// <summary>Reverses the links that close a cycle (found by depth-first search), so that layers can follow the link direction.</summary>
    private static List<(int From, int To)> ReverseBackLinks(int count, List<(int From, int To)> links)
    {
        var outgoing = links.ToLookup(l => l.From, l => l.To);
        var hasIncoming = links.Select(l => l.To).ToHashSet();
        var state = new VisitState[count];
        var backLinks = new HashSet<(int, int)>();

        void Visit(int box)
        {
            state[box] = VisitState.OnStack;
            foreach (var next in outgoing[box])
            {
                if (state[next] == VisitState.OnStack)
                {
                    backLinks.Add((box, next));
                }
                else if (state[next] == VisitState.Unvisited)
                {
                    Visit(next);
                }
            }
            state[box] = VisitState.Done;
        }

        // Start from sources so they end up in the top layer.
        foreach (var box in Enumerable.Range(0, count).OrderBy(hasIncoming.Contains))
        {
            if (state[box] == VisitState.Unvisited)
            {
                Visit(box);
            }
        }

        return links.Select(l => backLinks.Contains(l) ? (l.To, l.From) : l).Distinct().ToList();
    }

    /// <summary>Longest-path layering: each connected box goes one layer below its lowest predecessor.</summary>
    private static List<List<int>> AssignLayers(int count, List<(int From, int To)> links, HashSet<int> connected)
    {
        var layer = new int[count];
        var indegree = new int[count];
        foreach (var link in links)
        {
            indegree[link.To]++;
        }

        var outgoing = links.ToLookup(l => l.From, l => l.To);
        var queue = new Queue<int>(Enumerable.Range(0, count).Where(box => indegree[box] == 0));
        while (queue.TryDequeue(out var box))
        {
            foreach (var next in outgoing[box])
            {
                layer[next] = Math.Max(layer[next], layer[box] + 1);
                if (--indegree[next] == 0)
                {
                    queue.Enqueue(next);
                }
            }
        }

        return connected
            .Order()
            .GroupBy(box => layer[box])
            .OrderBy(g => g.Key)
            .Select(g => g.ToList())
            .ToList();
    }

    /// <summary>Barycenter heuristic: alternately sort each layer by the mean position of its neighbours in the adjacent layer.</summary>
    private static List<List<int>> OrderLayers(List<List<int>> layers, List<(int From, int To)> links)
    {
        var neighbours = links.SelectMany(l => new[] { (l.From, l.To), (l.To, l.From) }).ToLookup(p => p.Item1, p => p.Item2);
        var lastPair = Math.Max(0, layers.Count - 1);

        for (var sweep = 0; sweep < OrderingSweeps; sweep++)
        {
            var downwards = sweep % 2 == 0;
            var sequence = downwards ? Enumerable.Range(1, lastPair) : Enumerable.Range(0, lastPair).Reverse();
            foreach (var i in sequence)
            {
                layers[i] = ByBarycenter(layers[i], layers[downwards ? i - 1 : i + 1], neighbours);
            }
        }
        return layers;
    }

    /// <summary>Boxes without neighbours in the reference layer keep their current position as sort key.</summary>
    private static List<int> ByBarycenter(List<int> layer, List<int> referenceLayer, ILookup<int, int> neighbours)
    {
        var referencePosition = referenceLayer
            .Select((box, position) => (box, position))
            .ToDictionary(p => p.box, p => (double)p.position);
        return layer
            .Select((box, position) =>
            {
                var adjacent = neighbours[box].Where(referencePosition.ContainsKey).Select(n => referencePosition[n]).ToList();
                return (box, key: adjacent.Count > 0 ? adjacent.Average() : position);
            })
            .OrderBy(p => p.key)
            .Select(p => p.box)
            .ToList();
    }

    /// <summary>Places the layers as centered rows and returns the y below the last row.</summary>
    private static double PlaceLayers(IReadOnlyList<(double Width, double Height)> boxes, List<List<int>> layers, (double X, double Y)[] positions)
    {
        var rowWidths = layers.Select(row => row.Sum(box => boxes[box].Width) + ColumnGap * (row.Count - 1)).ToList();
        var maxWidth = rowWidths.DefaultIfEmpty(0).Max();
        var y = 0.0;

        for (var i = 0; i < layers.Count; i++)
        {
            var x = (maxWidth - rowWidths[i]) / 2;
            var rowHeight = layers[i].Max(box => boxes[box].Height);
            foreach (var box in layers[i])
            {
                positions[box] = (x, y + (rowHeight - boxes[box].Height) / 2);
                x += boxes[box].Width + ColumnGap;
            }
            y += rowHeight + RowGap;
        }
        return y;
    }

    private static void PlaceGrid(IReadOnlyList<(double Width, double Height)> boxes, List<int> items, double y, (double X, double Y)[] positions)
    {
        foreach (var row in items.Chunk(GridColumns))
        {
            var x = 0.0;
            foreach (var box in row)
            {
                positions[box] = (x, y);
                x += boxes[box].Width + ColumnGap;
            }
            y += row.Max(box => boxes[box].Height) + RowGap;
        }
    }
}
