namespace Kandinsky.Server.Model;

/// <summary>
/// A directed edge from the source shape to the target shape, drawn straight or angled. SourceCorner and TargetCorner
/// fix the diamond corner the edge is attached to at either end.
/// </summary>
public sealed record Edge(
    string Id,
    string SourceId,
    string TargetId,
    string? Label,
    EdgeRouting Routing = EdgeRouting.Straight,
    EdgeCorner SourceCorner = EdgeCorner.Auto,
    EdgeCorner TargetCorner = EdgeCorner.Auto)
{
    public bool HasFixedCorner() => SourceCorner != EdgeCorner.Auto || TargetCorner != EdgeCorner.Auto;
}
