using System.Text.RegularExpressions;
using Kandinsky.Server.Diagrams;
using Kandinsky.Server.Model;

namespace Kandinsky.Server.Formats;

/// <summary>Position and size of a shape, stored in a layout comment.</summary>
public sealed record ParsedBounds(double X, double Y, double Width, double Height);

/// <summary>A shape read from a text format; ParentId is the group (PlantUML package, Mermaid subgraph) it was declared in.</summary>
public sealed record ParsedShape(string Id, string Label, ParsedBounds? Bounds = null, ShapeKind Kind = ShapeKind.Rectangle, string? ParentId = null);

public sealed record ParsedEdge(
    string SourceId, string TargetId, string? Label, EdgeRouting Routing = EdgeRouting.Straight,
    EdgeCorner SourceCorner = EdgeCorner.Auto, EdgeCorner TargetCorner = EdgeCorner.Auto);

/// <summary>Diagram content read from a text format, with the shape layout if the text contains layout comments.</summary>
public sealed record ParsedDiagram(string? Title, IReadOnlyList<ParsedShape> Shapes, IReadOnlyList<ParsedEdge> Edges);

/// <param name="text">The whole text, so that generated ids avoid every id in it, also ones declared later.</param>
/// <param name="laterGroupsAdopt">
/// Whether a shape first mentioned outside any group joins the group it is mentioned in later (Mermaid),
/// or stays where it was first mentioned (PlantUML).
/// </param>
internal sealed partial class ParsedDiagramBuilder(string text, bool laterGroupsAdopt)
{
    private readonly HashSet<string> _words = WordPattern().Matches(text).Select(m => m.Value).ToHashSet();
    private readonly List<ParsedShape> _shapes = [];
    private readonly Dictionary<string, int> _shapeIndex = [];
    private readonly List<ParsedEdge> _edges = [];
    private readonly List<(int Line, string Id, ParsedBounds Bounds, ShapeKind? Kind)> _layout = [];
    private readonly List<(int Line, string Source, string Target, EdgeRouting Routing, EdgeCorner SourceCorner, EdgeCorner TargetCorner)> _routing = [];
    private readonly Stack<(int Line, string Id)> _groups = [];

    public string? Title { get; set; }

    /// <summary>The routing of edges without an edge comment, e.g. angled when the text asks for orthogonal lines everywhere.</summary>
    public EdgeRouting DefaultRouting { get; set; }

    /// <summary>
    /// Declares a shape or updates the label and kind of a declared one; null keeps the current value.
    /// A shape belongs to the group in which it first appears; see laterGroupsAdopt for shapes first mentioned outside groups.
    /// </summary>
    public void DeclareShape(string id, string? label, ShapeKind? kind = null)
    {
        _groups.TryPeek(out var group);
        if (_shapeIndex.TryGetValue(id, out var index))
        {
            var shape = _shapes[index];
            _shapes[index] = shape with
            {
                Label = label ?? shape.Label,
                // A group keeps its kind when it is referenced as a node, e.g. as an edge endpoint.
                Kind = shape.Kind == ShapeKind.Group ? shape.Kind : kind ?? shape.Kind,
                ParentId = shape.ParentId ?? (laterGroupsAdopt && group.Id is not null && !IsWithin(group.Id, id) ? group.Id : null),
            };
            return;
        }

        _shapeIndex[id] = _shapes.Count;
        _shapes.Add(new ParsedShape(id, label ?? id, null, kind ?? ShapeKind.Rectangle, group.Id));
    }

    /// <summary>Starts a group; the shapes declared until <see cref="EndGroup"/> belong to it. Without an id, one is generated.</summary>
    public void BeginGroup(int line, string? id, string? label)
    {
        id ??= NewGroupId();
        if (_groups.Any(g => g.Id == id))
        {
            throw Error(line, $"group '{id}' cannot be nested in itself.");
        }
        DeclareShape(id, string.IsNullOrWhiteSpace(label) ? null : label, ShapeKind.Group);
        _groups.Push((line, id));
    }

    public void EndGroup(int line, string closing)
    {
        if (!_groups.TryPop(out _))
        {
            throw Error(line, $"'{closing}' without an open group.");
        }
    }

    /// <summary>Is the shape the group itself or one of its ancestors? Then it cannot become the group's member.</summary>
    private bool IsWithin(string groupId, string id)
    {
        for (string? current = groupId; current is not null; current = _shapes[_shapeIndex[current]].ParentId)
        {
            if (current == id)
            {
                return true;
            }
        }
        return false;
    }

    private string NewGroupId()
    {
        for (var n = 1; ; n++)
        {
            var id = "group" + n;
            if (!_shapeIndex.ContainsKey(id) && !_words.Contains(id))
            {
                return id;
            }
        }
    }

    public void AddEdge(int line, string sourceId, string targetId, string? label)
    {
        if (sourceId == targetId)
        {
            throw Error(line, $"self-referencing edge on '{sourceId}' is not supported.");
        }

        DeclareShape(sourceId, null);
        DeclareShape(targetId, null);
        _edges.Add(new ParsedEdge(sourceId, targetId, string.IsNullOrWhiteSpace(label) ? null : label.Trim()));
    }

    /// <summary>Sets the bounds of a shape and, if given, its kind (for kinds the format can only approximate).</summary>
    public void SetLayout(int line, string id, ParsedBounds bounds, ShapeKind? kind = null) => _layout.Add((line, id, bounds, kind));

    /// <summary>Sets the routing and diamond corners of the next edge from source to target that has none set by a comment yet.</summary>
    public void SetEdgeRouting(
        int line, string source, string target, EdgeRouting routing, EdgeCorner sourceCorner = EdgeCorner.Auto, EdgeCorner targetCorner = EdgeCorner.Auto) =>
        _routing.Add((line, source, target, routing, sourceCorner, targetCorner));

    public ParsedDiagram Build()
    {
        if (_groups.TryPeek(out var open))
        {
            throw Error(open.Line, $"group '{open.Id}' is not closed.");
        }
        // Layout comments may appear anywhere, so they are applied once all shapes are known.
        foreach (var (line, id, bounds, kind) in _layout)
        {
            if (!_shapeIndex.TryGetValue(id, out var index))
            {
                throw Error(line, $"layout for unknown shape '{id}'.");
            }
            _shapes[index] = _shapes[index] with { Bounds = bounds, Kind = kind ?? _shapes[index].Kind };
        }

        var edges = _edges.Select(e => e with { Routing = DefaultRouting }).ToList();
        var routed = new HashSet<int>();
        foreach (var (line, source, target, routing, sourceCorner, targetCorner) in _routing)
        {
            var index = Enumerable.Range(0, edges.Count)
                .FirstOrDefault(i => !routed.Contains(i) && edges[i].SourceId == source && edges[i].TargetId == target, -1);
            if (index < 0)
            {
                throw Error(line, $"routing for unknown edge '{source}' -> '{target}'.");
            }
            routed.Add(index);
            edges[index] = edges[index] with { Routing = routing, SourceCorner = sourceCorner, TargetCorner = targetCorner };
        }
        return new(Title, _shapes, edges);
    }

    public static DiagramValidationException Error(int line, string message) => new($"Line {line}: {message}");

    [GeneratedRegex(@"\w+")]
    private static partial Regex WordPattern();
}
