using Kandinsky.Server.Diagrams;

namespace Kandinsky.Server.Formats;

public sealed record ParsedShape(string Id, string Label);

public sealed record ParsedEdge(string SourceId, string TargetId, string? Label);

/// <summary>Diagram content read from a text format, without layout.</summary>
public sealed record ParsedDiagram(string? Title, IReadOnlyList<ParsedShape> Shapes, IReadOnlyList<ParsedEdge> Edges);

internal sealed class ParsedDiagramBuilder
{
    private readonly List<ParsedShape> _shapes = [];
    private readonly Dictionary<string, int> _shapeIndex = [];
    private readonly List<ParsedEdge> _edges = [];

    public string? Title { get; set; }

    public void DeclareShape(string id, string? label)
    {
        if (_shapeIndex.TryGetValue(id, out var index))
        {
            if (label is not null)
            {
                _shapes[index] = _shapes[index] with { Label = label };
            }
            return;
        }

        _shapeIndex[id] = _shapes.Count;
        _shapes.Add(new ParsedShape(id, label ?? id));
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

    public ParsedDiagram Build() => new(Title, _shapes, _edges);

    public static DiagramValidationException Error(int line, string message) => new($"Line {line}: {message}");
}
