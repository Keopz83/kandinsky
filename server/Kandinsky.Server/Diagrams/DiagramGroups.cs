using Kandinsky.Server.Model;

namespace Kandinsky.Server.Diagrams;

/// <summary>Group membership follows the geometry: a shape belongs to the smallest group whose bounds fully contain it.</summary>
public static class DiagramGroups
{
    /// <summary>Sets every shape's ParentId from the current positions and sizes.</summary>
    public static IReadOnlyList<Shape> AssignParents(IReadOnlyList<Shape> shapes)
    {
        var parents = Parents(shapes);
        return shapes.Select(s => s.ParentId == parents[s.Id] ? s : s with { ParentId = parents[s.Id] }).ToList();
    }

    /// <summary>The parent group id of every shape (null at the top level).</summary>
    public static Dictionary<string, string?> Parents(IReadOnlyList<Shape> shapes)
    {
        // Groups ordered from outermost to innermost: larger first, the earlier one first for identical bounds.
        // A shape can only belong to a group ordered before it, so membership never forms a cycle.
        var rank = shapes
            .Select((s, i) => (s, i))
            .OrderByDescending(p => Area(p.s))
            .ThenBy(p => p.i)
            .Select((p, r) => (p.s.Id, r))
            .ToDictionary(p => p.Id, p => p.r);
        var groups = shapes.Where(s => s.Kind == ShapeKind.Group).OrderBy(g => rank[g.Id]).ToList();

        return shapes.ToDictionary(
            s => s.Id,
            s => groups.LastOrDefault(g => rank[g.Id] < rank[s.Id] && Contains(g, s))?.Id);
    }

    /// <summary>The ids of all shapes inside the given groups, at any depth.</summary>
    public static HashSet<string> Descendants(IReadOnlyList<Shape> shapes, IEnumerable<string> groupIds)
    {
        var children = shapes.Where(s => s.ParentId is not null).ToLookup(s => s.ParentId!, s => s.Id);
        var result = new HashSet<string>();
        var pending = new Stack<string>(groupIds);
        while (pending.TryPop(out var id))
        {
            foreach (var child in children[id])
            {
                if (result.Add(child))
                {
                    pending.Push(child);
                }
            }
        }
        return result;
    }

    private static double Area(Shape s) => s.Width * s.Height;

    private static bool Contains(Shape outer, Shape inner) =>
        inner.X >= outer.X && inner.Y >= outer.Y && inner.X + inner.Width <= outer.X + outer.Width && inner.Y + inner.Height <= outer.Y + outer.Height;
}
