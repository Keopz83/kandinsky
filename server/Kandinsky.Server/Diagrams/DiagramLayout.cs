using Kandinsky.Server.Model;

namespace Kandinsky.Server.Diagrams;

/// <summary>
/// Arranges a whole diagram with the <see cref="LayeredLayout"/>. The content of a group (shapes with that group as ParentId)
/// is laid out the same way inside the group, which is sized to fit it.
/// </summary>
public sealed class DiagramLayout
{
    private const double Origin = 40;
    private const double GroupPadding = 20;
    private const double GroupTitleHeight = 36;

    private readonly IReadOnlyList<Edge> _edges;
    private readonly Dictionary<string, Shape> _shapesById;
    private readonly Dictionary<string, string?> _parentIds;
    private readonly ILookup<string, Shape> _children;
    private readonly Dictionary<string, (double Width, double Height)> _sizes;
    // Position of each shape relative to the content origin of its container.
    private readonly Dictionary<string, (double X, double Y)> _offsets = [];
    private readonly Dictionary<string, Shape> _placed = [];

    private DiagramLayout(IReadOnlyList<Shape> shapes, IReadOnlyList<Edge> edges)
    {
        _edges = edges;
        _shapesById = shapes.ToDictionary(s => s.Id);
        _parentIds = shapes.ToDictionary(s => s.Id, s => IsGroup(s.ParentId) ? s.ParentId : null);
        BreakCycles(_parentIds);
        _children = shapes.Where(s => _parentIds[s.Id] is not null).ToLookup(s => _parentIds[s.Id]!);
        _sizes = shapes.ToDictionary(s => s.Id, s => (s.Width, s.Height));
    }

    public static IReadOnlyList<Shape> Arrange(IReadOnlyList<Shape> shapes, IReadOnlyList<Edge> edges)
    {
        var layout = new DiagramLayout(shapes, edges);
        layout.LayOutContent(null, shapes.Where(s => layout._parentIds[s.Id] is null).ToList());
        return shapes.Select(layout.Place).ToList();
    }

    private bool IsGroup(string? shapeId) =>
        shapeId is not null && _shapesById.TryGetValue(shapeId, out var shape) && shape.Kind == ShapeKind.Group;

    /// <summary>Lays out the content of a container (null for the top level) and returns the size of the content.</summary>
    private (double Width, double Height) LayOutContent(string? containerId, IReadOnlyList<Shape> content)
    {
        foreach (var group in content.Where(c => _children[c.Id].Any()))
        {
            var (width, height) = LayOutContent(group.Id, _children[group.Id].ToList());
            _sizes[group.Id] = (width + 2 * GroupPadding, height + GroupTitleHeight + GroupPadding);
        }

        var positions = LayeredLayout.Arrange(content.Select(s => _sizes[s.Id]).ToList(), LinksWithin(containerId, content));
        for (var i = 0; i < content.Count; i++)
        {
            _offsets[content[i].Id] = positions[i];
        }
        return Extent(content, positions);
    }

    /// <summary>The edges between the content shapes of a container, as index pairs; edges into nested groups count for the group.</summary>
    private List<(int From, int To)> LinksWithin(string? containerId, IReadOnlyList<Shape> content)
    {
        var indexById = content.Select((s, i) => (s.Id, i)).ToDictionary(p => p.Id, p => p.i);
        return _edges
            .Select(e => (From: ContentShapeOf(containerId, e.SourceId), To: ContentShapeOf(containerId, e.TargetId)))
            .Where(link => link.From is not null && link.To is not null && link.From != link.To)
            .Select(link => (indexById[link.From!], indexById[link.To!]))
            .ToList();
    }

    /// <summary>The content shape of the container that is the shape itself or contains it, if any.</summary>
    private string? ContentShapeOf(string? containerId, string shapeId)
    {
        for (string? current = shapeId; current is not null; current = _parentIds[current])
        {
            if (_parentIds[current] == containerId)
            {
                return current;
            }
        }
        return null;
    }

    private (double Width, double Height) Extent(IReadOnlyList<Shape> content, (double X, double Y)[] positions)
    {
        if (content.Count == 0)
        {
            return (0, 0);
        }
        var width = content.Select((s, i) => positions[i].X + _sizes[s.Id].Width).Max();
        var height = content.Select((s, i) => positions[i].Y + _sizes[s.Id].Height).Max();
        return (width, height);
    }

    /// <summary>Turns the relative offsets into canvas positions; a group is placed before its content.</summary>
    private Shape Place(Shape shape)
    {
        if (_placed.TryGetValue(shape.Id, out var placed))
        {
            return placed;
        }
        var (x, y) = _offsets[shape.Id];
        var (originX, originY) = _parentIds[shape.Id] is { } groupId
            ? ContentOrigin(Place(_shapesById[groupId]))
            : (Origin, Origin);
        var (width, height) = _sizes[shape.Id];
        return _placed[shape.Id] = shape with { X = Math.Round(originX + x), Y = Math.Round(originY + y), Width = width, Height = height };
    }

    private static (double X, double Y) ContentOrigin(Shape group) => (group.X + GroupPadding, group.Y + GroupTitleHeight);

    /// <summary>Moves shapes whose parent chain loops back to them to the top level.</summary>
    private static void BreakCycles(Dictionary<string, string?> parentIds)
    {
        foreach (var id in parentIds.Keys.ToList())
        {
            var seen = new HashSet<string> { id };
            for (var current = parentIds[id]; current is not null; current = parentIds[current])
            {
                if (!seen.Add(current))
                {
                    parentIds[id] = null;
                    break;
                }
            }
        }
    }
}
