using Kandinsky.Server.Formats;
using Kandinsky.Server.Model;

namespace Kandinsky.Server.Diagrams;

/// <summary>A diagram read from text, not registered yet, with the title given in the text.</summary>
internal sealed record TextDiagram(string? Title, Diagram Diagram);

/// <summary>Reads PlantUML or Mermaid text into an unregistered diagram, laid out as on import (R-IO-7).</summary>
internal static class TextDiagramReader
{
    public static TextDiagram Read(string? text, string? formatName)
    {
        var cleanText = DiagramFormats.Unfence(DiagramFormats.Normalize(DiagramInput.ValidImportText(text)));
        var format = formatName is null ? DiagramFormats.Detect(cleanText) : DiagramFormats.Parse(formatName);
        var parsed = DiagramFormats.Parse(cleanText, format);

        var edges = ToEdges(parsed.Shapes, parsed.Edges);
        var shapes = parsed.Shapes.All(s => s.Bounds is null)
            ? AutoLaidOut(parsed.Shapes, edges)
            : KeepingLayout(parsed.Shapes);
        return new TextDiagram(parsed.Title, new Diagram("", "", shapes, edges));
    }

    private static List<Edge> ToEdges(IReadOnlyList<ParsedShape> shapes, IReadOnlyList<ParsedEdge> edges)
    {
        var ids = new ElementIdAllocator(shapes.Select(s => s.Id));
        return edges
            .Select(e => new Edge(
                ids.NextEdgeId(), e.SourceId, e.TargetId, DiagramInput.ValidEdgeLabel(e.Label), e.Routing, e.SourceCorner, e.TargetCorner))
            .ToList();
    }

    // Auto-layout keeps the nesting of the text; afterwards membership follows the positions as usual.
    private static IReadOnlyList<Shape> AutoLaidOut(IReadOnlyList<ParsedShape> parsedShapes, IReadOnlyList<Edge> edges)
    {
        var shapes = parsedShapes
            .Select(s => WithDefaultSize(s, 0, 0, s.ParentId))
            .ToList();
        return DiagramLayout.Arrange(shapes, edges);
    }

    /// <summary>Shapes with a layout comment keep their position; shapes added without one go to free grid slots.</summary>
    private static IReadOnlyList<Shape> KeepingLayout(IReadOnlyList<ParsedShape> parsedShapes)
    {
        var placed = parsedShapes
            .Where(s => s.Bounds is not null)
            .Select(s => WithBounds(s, s.Bounds!))
            .ToDictionary(s => s.Id);
        foreach (var shape in parsedShapes.Where(s => s.Bounds is null))
        {
            var (width, height) = ShapeDefaults.Size(shape.Kind);
            var (x, y) = FreeGridSlot.Find(placed.Values, width, height);
            placed[shape.Id] = WithDefaultSize(shape, x, y);
        }
        return parsedShapes.Select(s => placed[s.Id]).ToList();
    }

    private static Shape WithBounds(ParsedShape shape, ParsedBounds bounds) => new(
        shape.Id,
        DiagramInput.ValidLabel(shape.Label),
        DiagramInput.ValidCoordinate(bounds.X, "X"),
        DiagramInput.ValidCoordinate(bounds.Y, "Y"),
        DiagramInput.ValidSize(bounds.Width, "Width"),
        DiagramInput.ValidSize(bounds.Height, "Height"),
        shape.Kind);

    private static Shape WithDefaultSize(ParsedShape shape, double x, double y, string? parentId = null)
    {
        var (width, height) = ShapeDefaults.Size(shape.Kind);
        return new Shape(shape.Id, DiagramInput.ValidLabel(shape.Label), x, y, width, height, shape.Kind, parentId);
    }
}
