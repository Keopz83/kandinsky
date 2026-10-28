using Kandinsky.Server.Model;

namespace Kandinsky.Server.Formats;

/// <summary>
/// The PlantUML element keyword of each shape kind. PlantUML class and deployment diagrams have no start/end points, parallelograms,
/// subroutines or trapezoids, so these are approximated; the layout comment then keeps the exact kind.
/// </summary>
internal static class PlantUmlElements
{
    public static string Keyword(ShapeKind kind) => kind switch
    {
        ShapeKind.Database => "database",
        // PlantUML has no labeled diamond outside activity diagrams; a hexagon shows the label.
        ShapeKind.Decision or ShapeKind.Preparation => "hexagon",
        ShapeKind.Group => "package",
        ShapeKind.Start or ShapeKind.End or ShapeKind.Connector => "circle",
        ShapeKind.Terminal => "usecase",
        ShapeKind.Rounded => "storage",
        ShapeKind.Document => "file",
        ShapeKind.InputOutput or ShapeKind.Subroutine or ShapeKind.Manual => "rectangle",
        _ => "class",
    };

    /// <summary>The kind an element is imported as, unless a layout comment says otherwise.</summary>
    public static ShapeKind ImportedKind(string keyword) => keyword switch
    {
        "database" => ShapeKind.Database,
        "hexagon" or "diamond" or "<>" => ShapeKind.Decision,
        "circle" => ShapeKind.Start,
        "usecase" => ShapeKind.Terminal,
        "storage" => ShapeKind.Rounded,
        "file" => ShapeKind.Document,
        _ => ShapeKind.Rectangle,
    };

    /// <summary>Whether the written element is read back as another kind, so that the layout comment must carry the kind.</summary>
    public static bool IsApproximated(ShapeKind kind) => kind != ShapeKind.Group && ImportedKind(Keyword(kind)) != kind;
}
