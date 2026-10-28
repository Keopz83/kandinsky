using System.Text.Json.Serialization;

namespace Kandinsky.Server.Model;

/// <summary>
/// How a shape is drawn: a plain rectangle (process), a database cylinder, a decision diamond, a group container,
/// or one of the other flowchart symbols.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ShapeKind>))]
public enum ShapeKind
{
    [JsonStringEnumMemberName("rectangle")]
    Rectangle,

    [JsonStringEnumMemberName("database")]
    Database,

    [JsonStringEnumMemberName("decision")]
    Decision,

    [JsonStringEnumMemberName("group")]
    Group,

    /// <summary>Start point: a small filled circle.</summary>
    [JsonStringEnumMemberName("start")]
    Start,

    /// <summary>End (stop) point: a framed circle.</summary>
    [JsonStringEnumMemberName("end")]
    End,

    /// <summary>Terminal point: a stadium (pill).</summary>
    [JsonStringEnumMemberName("terminal")]
    Terminal,

    /// <summary>Event: a rounded rectangle.</summary>
    [JsonStringEnumMemberName("rounded")]
    Rounded,

    /// <summary>Data input / output: a parallelogram.</summary>
    [JsonStringEnumMemberName("io")]
    InputOutput,

    /// <summary>Predefined process: a rectangle with double vertical sides.</summary>
    [JsonStringEnumMemberName("subroutine")]
    Subroutine,

    /// <summary>Document: a rectangle with a wavy bottom.</summary>
    [JsonStringEnumMemberName("document")]
    Document,

    /// <summary>Connector: a labeled circle.</summary>
    [JsonStringEnumMemberName("connector")]
    Connector,

    /// <summary>Preparation: a hexagon.</summary>
    [JsonStringEnumMemberName("preparation")]
    Preparation,

    /// <summary>Manual operation: a trapezoid, wider at the top.</summary>
    [JsonStringEnumMemberName("manual")]
    Manual,
}

/// <summary>How an edge is drawn: a straight line or an angled (orthogonal) path of horizontal and vertical segments.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EdgeRouting>))]
public enum EdgeRouting
{
    [JsonStringEnumMemberName("straight")]
    Straight,

    [JsonStringEnumMemberName("angled")]
    Angled,
}

/// <summary>
/// A shape on the diagram canvas, drawn within its bounds. ParentId is the group the shape lies in
/// (the smallest group whose bounds fully contain it); the server derives it from the positions, so it is read-only.
/// </summary>
public sealed record Shape(string Id, string Label, double X, double Y, double Width, double Height, ShapeKind Kind = ShapeKind.Rectangle, string? ParentId = null);

/// <summary>A directed edge from the source shape to the target shape, drawn straight or angled.</summary>
public sealed record Edge(string Id, string SourceId, string TargetId, string? Label, EdgeRouting Routing = EdgeRouting.Straight);

/// <summary>A diagram with its shapes and edges.</summary>
public sealed record Diagram(string Id, string Name, IReadOnlyList<Shape> Shapes, IReadOnlyList<Edge> Edges);

/// <summary>The diagram after a paste and the ids of the added shapes and edges.</summary>
public sealed record PasteResult(Diagram Diagram, IReadOnlyList<string> ShapeIds, IReadOnlyList<string> EdgeIds);

/// <summary>Id and name of a diagram, used in diagram lists.</summary>
public sealed record DiagramSummary(string Id, string Name);
