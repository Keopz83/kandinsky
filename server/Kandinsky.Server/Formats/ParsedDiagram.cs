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
