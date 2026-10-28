using System.Text.RegularExpressions;
using Kandinsky.Server.Diagrams;
using Kandinsky.Server.Model;

namespace Kandinsky.Server.Formats;

/// <summary>Collects the statements of a text format, in text order, into a <see cref="ParsedDiagram"/>.</summary>
/// <param name="text">The whole text, so that generated ids avoid every id in it, also ones declared later.</param>
/// <param name="laterGroupsAdopt">
/// Whether a shape first mentioned outside any group joins the group it is mentioned in later (Mermaid),
/// or stays where it was first mentioned (PlantUML).
/// </param>
internal sealed partial class ParsedDiagramBuilder(string text, bool laterGroupsAdopt)
{
    private const string GroupIdPrefix = "group";

    private sealed record PendingLayout(int Line, string ShapeId, ParsedBounds Bounds, ShapeKind? Kind);

    private sealed record PendingRouting(
        int Line, string SourceId, string TargetId, EdgeRouting Routing, EdgeCorner SourceCorner, EdgeCorner TargetCorner);

    private readonly HashSet<string> _wordsInText = WordPattern().Matches(text).Select(m => m.Value).ToHashSet();
    private readonly List<ParsedShape> _shapes = [];
    private readonly Dictionary<string, int> _shapeIndex = [];
    private readonly List<ParsedEdge> _edges = [];
    private readonly List<PendingLayout> _layouts = [];
    private readonly List<PendingRouting> _routings = [];
    private readonly Stack<(int Line, string Id)> _openGroups = [];

    public string? Title { get; set; }

    /// <summary>The routing of edges without an edge comment, e.g. angled when the text asks for orthogonal lines everywhere.</summary>
    public EdgeRouting DefaultRouting { get; set; }

    public static DiagramValidationException Error(int line, string message) => new($"Line {line}: {message}");

    /// <summary>
    /// Declares a shape or updates the label and kind of a declared one; null keeps the current value.
    /// A shape belongs to the group in which it first appears; see laterGroupsAdopt for shapes first mentioned outside groups.
    /// </summary>
    public void DeclareShape(string id, string? label, ShapeKind? kind = null)
    {
        var groupId = _openGroups.TryPeek(out var group) ? group.Id : null;
        if (!_shapeIndex.TryGetValue(id, out var index))
        {
            _shapeIndex[id] = _shapes.Count;
            _shapes.Add(new ParsedShape(id, label ?? id, null, kind ?? ShapeKind.Rectangle, groupId));
            return;
        }

        var shape = _shapes[index];
        _shapes[index] = shape with
        {
            Label = label ?? shape.Label,
            // A group keeps its kind when it is referenced as a node, e.g. as an edge endpoint.
            Kind = shape.Kind == ShapeKind.Group ? shape.Kind : kind ?? shape.Kind,
            ParentId = shape.ParentId ?? AdoptingGroup(groupId, id),
        };
    }

    /// <summary>Starts a group; the shapes declared until <see cref="EndGroup"/> belong to it. Without an id, one is generated.</summary>
    public void BeginGroup(int line, string? id, string? label)
    {
        id ??= NewGroupId();
        if (_openGroups.Any(g => g.Id == id))
        {
            throw Error(line, $"group '{id}' cannot be nested in itself.");
        }
        DeclareShape(id, string.IsNullOrWhiteSpace(label) ? null : label, ShapeKind.Group);
        _openGroups.Push((line, id));
    }

    public void EndGroup(int line, string closing)
    {
        if (!_openGroups.TryPop(out _))
        {
            throw Error(line, $"'{closing}' without an open group.");
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
    public void SetLayout(int line, string id, ParsedBounds bounds, ShapeKind? kind = null) => _layouts.Add(new(line, id, bounds, kind));

    /// <summary>Sets the routing and diamond corners of the next edge from source to target that has none set by a comment yet.</summary>
    public void SetEdgeRouting(
        int line, string source, string target, EdgeRouting routing, EdgeCorner sourceCorner = EdgeCorner.Auto, EdgeCorner targetCorner = EdgeCorner.Auto) =>
        _routings.Add(new(line, source, target, routing, sourceCorner, targetCorner));

    public ParsedDiagram Build()
    {
        if (_openGroups.TryPeek(out var open))
        {
            throw Error(open.Line, $"group '{open.Id}' is not closed.");
        }
        // Layout and edge comments may appear anywhere, so they are applied once all shapes and edges are known.
        ApplyLayouts();
        return new(Title, _shapes, RoutedEdges());
    }

    private string? AdoptingGroup(string? groupId, string shapeId) =>
        laterGroupsAdopt && groupId is not null && !IsWithin(groupId, shapeId) ? groupId : null;

    /// <summary>Is the shape the group itself or one of its ancestors? Then it cannot become the group's member.</summary>
    private bool IsWithin(string groupId, string shapeId)
    {
        for (string? current = groupId; current is not null; current = _shapes[_shapeIndex[current]].ParentId)
        {
            if (current == shapeId)
            {
                return true;
            }
        }
        return false;
    }

    private string NewGroupId()
    {
        for (var number = 1; ; number++)
        {
            var id = GroupIdPrefix + number;
            if (!_shapeIndex.ContainsKey(id) && !_wordsInText.Contains(id))
            {
                return id;
            }
        }
    }

    private void ApplyLayouts()
    {
        foreach (var layout in _layouts)
        {
            if (!_shapeIndex.TryGetValue(layout.ShapeId, out var index))
            {
                throw Error(layout.Line, $"layout for unknown shape '{layout.ShapeId}'.");
            }
            _shapes[index] = _shapes[index] with { Bounds = layout.Bounds, Kind = layout.Kind ?? _shapes[index].Kind };
        }
    }

    private List<ParsedEdge> RoutedEdges()
    {
        var edges = _edges.Select(e => e with { Routing = DefaultRouting }).ToList();
        var routedIndexes = new HashSet<int>();
        foreach (var routing in _routings)
        {
            var index = Enumerable.Range(0, edges.Count).FirstOrDefault(
                i => !routedIndexes.Contains(i) && edges[i].SourceId == routing.SourceId && edges[i].TargetId == routing.TargetId,
                -1);
            if (index < 0)
            {
                throw Error(routing.Line, $"routing for unknown edge '{routing.SourceId}' -> '{routing.TargetId}'.");
            }
            routedIndexes.Add(index);
            edges[index] = edges[index] with { Routing = routing.Routing, SourceCorner = routing.SourceCorner, TargetCorner = routing.TargetCorner };
        }
        return edges;
    }

    [GeneratedRegex(@"\w+")]
    private static partial Regex WordPattern();
}
