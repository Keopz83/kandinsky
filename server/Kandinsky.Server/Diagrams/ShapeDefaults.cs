using Kandinsky.Server.Model;

namespace Kandinsky.Server.Diagrams;

/// <summary>The size and label of a shape added without them.</summary>
public static class ShapeDefaults
{
    private const double StandardWidth = 140;
    private const double StandardHeight = 60;
    // Decisions are taller to fit a label in the diamond.
    private const double DecisionHeight = 80;
    private const double DocumentHeight = 70;
    // Groups leave room for content.
    private const double GroupWidth = 320;
    private const double GroupHeight = 200;
    private const double PointDiameter = 40;
    private const double ConnectorDiameter = 60;

    public static (double Width, double Height) Size(ShapeKind kind) => kind switch
    {
        ShapeKind.Decision => (StandardWidth, DecisionHeight),
        ShapeKind.Group => (GroupWidth, GroupHeight),
        ShapeKind.Start or ShapeKind.End => (PointDiameter, PointDiameter),
        ShapeKind.Connector => (ConnectorDiameter, ConnectorDiameter),
        ShapeKind.Document => (StandardWidth, DocumentHeight),
        _ => (StandardWidth, StandardHeight),
    };

    public static string Label(ShapeKind kind) => kind switch
    {
        ShapeKind.Database => "Database",
        ShapeKind.Decision => "Decision",
        ShapeKind.Group => "Group",
        ShapeKind.Start => "Start",
        ShapeKind.End => "End",
        ShapeKind.Terminal => "Terminal",
        ShapeKind.Rounded => "Event",
        ShapeKind.InputOutput => "Input / output",
        ShapeKind.Subroutine => "Subroutine",
        ShapeKind.Document => "Document",
        ShapeKind.Connector => "A",
        ShapeKind.Preparation => "Preparation",
        ShapeKind.Manual => "Manual operation",
        _ => "Class",
    };
}
