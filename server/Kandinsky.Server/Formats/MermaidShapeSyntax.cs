using Kandinsky.Server.Model;

namespace Kandinsky.Server.Formats;

/// <summary>
/// How shape kinds are written in Mermaid flowcharts: the classic bracket syntax (<c>[(text)]</c>) where it exists,
/// else the v11 shape syntax (<c>A@{ shape: doc }</c>).
/// </summary>
internal static class MermaidShapeSyntax
{
    /// <summary>The classic node brackets of a kind, or null for kinds that need the v11 shape syntax.</summary>
    public static (string Open, string Close)? ClassicBrackets(ShapeKind kind) => kind switch
    {
        ShapeKind.Rectangle => ("[", "]"),
        ShapeKind.Database => ("[(", ")]"),
        ShapeKind.Decision => ("{", "}"),
        ShapeKind.Rounded => ("(", ")"),
        ShapeKind.Terminal => ("([", "])"),
        ShapeKind.Subroutine => ("[[", "]]"),
        ShapeKind.Connector => ("((", "))"),
        ShapeKind.Preparation => ("{{", "}}"),
        ShapeKind.InputOutput => ("[/", "/]"),
        ShapeKind.Manual => ("[\\", "/]"),
        _ => null,
    };

    public static string V11ShapeName(ShapeKind kind) => kind switch
    {
        ShapeKind.Start => "sm-circ",
        ShapeKind.End => "fr-circ",
        ShapeKind.Document => "doc",
        _ => "rect",
    };

    /// <summary>Kinds of the classic node syntax by opening and closing bracket; other combinations are rectangles.</summary>
    public static ShapeKind ClassicKind(string open, string close) => (open, close) switch
    {
        ("[(", _) => ShapeKind.Database,
        ("{", _) => ShapeKind.Decision,
        ("(", _) => ShapeKind.Rounded,
        ("([", _) => ShapeKind.Terminal,
        ("[[", _) => ShapeKind.Subroutine,
        ("((", _) => ShapeKind.Connector,
        ("(((", _) => ShapeKind.End,
        ("{{", _) => ShapeKind.Preparation,
        ("[/", "/]") or ("[\\", "\\]") => ShapeKind.InputOutput,
        ("[\\", "/]") => ShapeKind.Manual,
        _ => ShapeKind.Rectangle,
    };

    /// <summary>Kinds of the v11 shape names and aliases; other shapes are rectangles.</summary>
    public static ShapeKind V11Kind(string shapeName) => shapeName switch
    {
        "rounded" or "event" => ShapeKind.Rounded,
        "stadium" or "pill" or "terminal" => ShapeKind.Terminal,
        "fr-rect" or "framed-rectangle" or "subproc" or "subprocess" or "subroutine" => ShapeKind.Subroutine,
        "cyl" or "cylinder" or "database" or "db" => ShapeKind.Database,
        "circle" or "circ" => ShapeKind.Connector,
        "sm-circ" or "small-circle" or "start" or "f-circ" or "filled-circle" or "junction" => ShapeKind.Start,
        "fr-circ" or "framed-circle" or "stop" or "dbl-circ" or "double-circle" => ShapeKind.End,
        "diam" or "decision" or "diamond" or "question" => ShapeKind.Decision,
        "hex" or "hexagon" or "prepare" => ShapeKind.Preparation,
        "lean-r" or "lean-right" or "in-out" or "lean-l" or "lean-left" or "out-in" => ShapeKind.InputOutput,
        "trap-t" or "inv-trapezoid" or "manual" or "trapezoid-top" => ShapeKind.Manual,
        "doc" or "document" => ShapeKind.Document,
        _ => ShapeKind.Rectangle,
    };

    // Double quotes would end the quoted label.
    public static string QuotableLabel(string label) => label.Replace('"', '\'');
}
