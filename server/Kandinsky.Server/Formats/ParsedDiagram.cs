using Kandinsky.Server.Diagrams;
using Kandinsky.Server.Model;

namespace Kandinsky.Server.Formats;

/// <summary>Position and size of a shape, stored in a layout comment.</summary>
public sealed record ParsedBounds(double X, double Y, double Width, double Height);

public sealed record ParsedShape(string Id, string Label, ParsedBounds? Bounds = null, ShapeKind Kind = ShapeKind.Rectangle);

public sealed record ParsedEdge(string SourceId, string TargetId, string? Label);

/// <summary>Diagram content read from a text format, with the shape layout if the text contains layout comments.</summary>
public sealed record ParsedDiagram(string? Title, IReadOnlyList<ParsedShape> Shapes, IReadOnlyList<ParsedEdge> Edges);

internal sealed class ParsedDiagramBuilder
{
    private readonly List<ParsedShape> _shapes = [];
    private readonly Dictionary<string, int> _shapeIndex = [];
    private readonly List<ParsedEdge> _edges = [];
    private readonly List<(int Line, string Id, ParsedBounds Bounds)> _layout = [];

    public string? Title { get; set; }

    /// <summary>Declares a shape or updates the label and kind of a declared one; null keeps the current value.</summary>
    public void DeclareShape(string id, string? label, ShapeKind? kind = null)
    {
        if (_shapeIndex.TryGetValue(id, out var index))
        {
            var shape = _shapes[index];
            _shapes[index] = shape with { Label = label ?? shape.Label, Kind = kind ?? shape.Kind };
            return;
        }

        _shapeIndex[id] = _shapes.Count;
        _shapes.Add(new ParsedShape(id, label ?? id, null, kind ?? ShapeKind.Rectangle));
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

    public void SetLayout(int line, string id, ParsedBounds bounds) => _layout.Add((line, id, bounds));

    public ParsedDiagram Build()
    {
        // Layout comments may appear anywhere, so they are applied once all shapes are known.
        foreach (var (line, id, bounds) in _layout)
        {
            if (!_shapeIndex.TryGetValue(id, out var index))
            {
                throw Error(line, $"layout for unknown shape '{id}'.");
            }
            _shapes[index] = _shapes[index] with { Bounds = bounds };
        }
        return new(Title, _shapes, _edges);
    }

    public static DiagramValidationException Error(int line, string message) => new($"Line {line}: {message}");
}
