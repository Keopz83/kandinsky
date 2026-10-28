using Kandinsky.Server.Model;

namespace Kandinsky.Server.Diagrams;

/// <summary>
/// Adds pasted content to a diagram as new shapes and edges, keeping its relative layout: centered on a point,
/// else placed below the existing shapes.
/// </summary>
internal static class DiagramPaste
{
    private const double GapBelowExistingShapes = 80;

    public static PasteResult Merge(Diagram diagram, Diagram pasted, double? centerX, double? centerY)
    {
        var ids = ElementIdAllocator.For(diagram);
        // Pasted shapes are new shapes: ids already taken in the diagram are replaced.
        var shapeIds = pasted.Shapes.ToDictionary(s => s.Id, s => ids.ClaimShapeId(s.Id));
        var (deltaX, deltaY) = Offset(diagram, pasted.Shapes, centerX, centerY);

        var shapes = pasted.Shapes
            .Select(s => s with
            {
                Id = shapeIds[s.Id],
                X = DiagramInput.ValidCoordinate(s.X + deltaX, "X"),
                Y = DiagramInput.ValidCoordinate(s.Y + deltaY, "Y"),
            })
            .ToList();
        var edges = pasted.Edges
            .Select(e => e with { Id = ids.NextEdgeId(), SourceId = shapeIds[e.SourceId], TargetId = shapeIds[e.TargetId] })
            .ToList();

        var merged = diagram with { Shapes = [.. diagram.Shapes, .. shapes], Edges = [.. diagram.Edges, .. edges] };
        return new PasteResult(merged, shapes.Select(s => s.Id).ToList(), edges.Select(e => e.Id).ToList());
    }

    private static (double X, double Y) Offset(Diagram diagram, IReadOnlyList<Shape> pasted, double? centerX, double? centerY)
    {
        var left = pasted.Min(s => s.X);
        var top = pasted.Min(s => s.Y);
        var (deltaX, deltaY) = centerX is { } x && centerY is { } y
            ? (x - (left + pasted.Max(s => s.X + s.Width)) / 2, y - (top + pasted.Max(s => s.Y + s.Height)) / 2)
            : (0, OffsetBelowExistingShapes(diagram, top));
        // Pasted shapes never move past the top-left edge of the canvas.
        return (Math.Max(Math.Round(deltaX), -left), Math.Max(Math.Round(deltaY), -top));
    }

    private static double OffsetBelowExistingShapes(Diagram diagram, double pastedTop) =>
        diagram.Shapes.Count == 0 ? 0 : diagram.Shapes.Max(s => s.Y + s.Height) + GapBelowExistingShapes - pastedTop;
}
