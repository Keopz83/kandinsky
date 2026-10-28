using System.Text.Json.Serialization;

namespace Kandinsky.Server.Model;

/// <summary>How a shape is drawn: a plain rectangle or a database cylinder.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ShapeKind>))]
public enum ShapeKind
{
    [JsonStringEnumMemberName("rectangle")]
    Rectangle,

    [JsonStringEnumMemberName("database")]
    Database,
}

/// <summary>A shape on the diagram canvas: a rectangle or a database within its bounds.</summary>
public sealed record Shape(string Id, string Label, double X, double Y, double Width, double Height, ShapeKind Kind = ShapeKind.Rectangle);

/// <summary>A directed edge from the source shape to the target shape.</summary>
public sealed record Edge(string Id, string SourceId, string TargetId, string? Label);

/// <summary>A diagram with its shapes and edges.</summary>
public sealed record Diagram(string Id, string Name, IReadOnlyList<Shape> Shapes, IReadOnlyList<Edge> Edges);

/// <summary>The diagram after a paste and the ids of the added shapes and edges.</summary>
public sealed record PasteResult(Diagram Diagram, IReadOnlyList<string> ShapeIds, IReadOnlyList<string> EdgeIds);

/// <summary>Id and name of a diagram, used in diagram lists.</summary>
public sealed record DiagramSummary(string Id, string Name);
