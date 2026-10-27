namespace Kandinsky.Server.Model;

/// <summary>A rectangle on the diagram canvas.</summary>
public sealed record Shape(string Id, string Label, double X, double Y, double Width, double Height);

/// <summary>A directed edge from the source shape to the target shape.</summary>
public sealed record Edge(string Id, string SourceId, string TargetId, string? Label);

/// <summary>A diagram with its shapes and edges.</summary>
public sealed record Diagram(string Id, string Name, IReadOnlyList<Shape> Shapes, IReadOnlyList<Edge> Edges);

/// <summary>Id and name of a diagram, used in diagram lists.</summary>
public sealed record DiagramSummary(string Id, string Name);
