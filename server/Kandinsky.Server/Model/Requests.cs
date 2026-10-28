namespace Kandinsky.Server.Model;

/// <summary>Payload for creating a diagram.</summary>
public sealed record CreateDiagramRequest(string? Name);

/// <summary>Payload for renaming a diagram.</summary>
public sealed record UpdateDiagramRequest(string Name);

/// <summary>Payload for adding a rectangle. Without a position the server places it in a free spot.</summary>
public sealed record CreateShapeRequest(string? Label, double? X, double? Y, double? Width, double? Height);

/// <summary>Partial update of a rectangle. Omitted properties stay unchanged.</summary>
public sealed record UpdateShapeRequest(string? Label, double? X, double? Y, double? Width, double? Height);

/// <summary>Payload for adding a directed edge between two existing rectangles.</summary>
public sealed record CreateEdgeRequest(string SourceId, string TargetId, string? Label);

/// <summary>Partial update of an edge. Omitted properties stay unchanged; an empty label removes the label.</summary>
public sealed record UpdateEdgeRequest(string? Label, string? SourceId, string? TargetId);

/// <summary>
/// Payload for importing a diagram. Format is "plantuml" or "mermaid"; detected from the text when omitted.
/// Name overrides the title in the text; FallbackName (e.g. the file name) is used only when the text has no title.
/// </summary>
public sealed record ImportDiagramRequest(string Text, string? Format, string? Name, string? FallbackName = null);

/// <summary>
/// Payload for pasting PlantUML or Mermaid text into an existing diagram. Format is detected from the text when omitted.
/// The pasted shapes are centered on (CenterX, CenterY); without a center they are placed below the existing shapes.
/// </summary>
public sealed record PasteDiagramRequest(string Text, string? Format, double? CenterX, double? CenterY);

public enum AlignDirection
{
    /// <summary>In a row: vertical centers match the first shape's.</summary>
    Horizontal,

    /// <summary>In a column: horizontal centers match the first shape's.</summary>
    Vertical,
}

/// <summary>Payload for aligning shapes. The first shape is the anchor the others are aligned to.</summary>
public sealed record AlignShapesRequest(IReadOnlyList<string>? ShapeIds, AlignDirection? Direction);
