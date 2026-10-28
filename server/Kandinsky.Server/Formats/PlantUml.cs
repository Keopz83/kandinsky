using System.Text;
using System.Text.RegularExpressions;
using Kandinsky.Server.Diagrams;
using Kandinsky.Server.Model;

namespace Kandinsky.Server.Formats;

/// <summary>
/// PlantUML class diagram subset: classes (and similar elements), databases, decisions (hexagons), flowchart symbols
/// (as the closest deployment elements), groups (packages and other containers) and directed associations.
/// </summary>
public static partial class PlantUml
{
    /// <summary>
    /// The element written for each shape kind. PlantUML class and deployment diagrams have no start/end points, parallelograms,
    /// subroutines or trapezoids, so these are approximated; the layout comment then keeps the exact kind (see <see cref="ImportedKind"/>).
    /// </summary>
    private static string Keyword(ShapeKind kind) => kind switch
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
    private static ShapeKind ImportedKind(string keyword) => keyword switch
    {
        "database" => ShapeKind.Database,
        "hexagon" or "diamond" or "<>" => ShapeKind.Decision,
        "circle" => ShapeKind.Start,
        "usecase" => ShapeKind.Terminal,
        "storage" => ShapeKind.Rounded,
        "file" => ShapeKind.Document,
        _ => ShapeKind.Rectangle,
    };

    public static string Export(Diagram diagram)
    {
        var text = new StringBuilder();
        text.Append("@startuml\n");
        // Class diagrams accept deployment elements such as database or hexagon only with mixing allowed.
        if (diagram.Shapes.Any(s => s.Kind is not (ShapeKind.Rectangle or ShapeKind.Group)))
        {
            text.Append("allowmixing\n");
        }
        var allAngled = diagram.Edges.Count > 0 && diagram.Edges.All(e => e.Routing == EdgeRouting.Angled);
        if (allAngled)
        {
            text.Append("skinparam linetype ortho\n");
        }
        var parents = DiagramGroups.Parents(diagram.Shapes);
        var children = diagram.Shapes.ToLookup(s => parents[s.Id] ?? "");
        void AppendShapes(string parentId, string indent)
        {
            foreach (var shape in children[parentId])
            {
                text.Append($"{indent}{Keyword(shape.Kind)} \"{shape.Label.Replace('"', '\'')}\" as {shape.Id}");
                if (shape.Kind == ShapeKind.Group)
                {
                    text.Append(" {\n");
                    AppendShapes(shape.Id, indent + "  ");
                    text.Append(indent).Append('}');
                }
                text.Append('\n');
            }
        }
        AppendShapes("", "");
        foreach (var edge in diagram.Edges)
        {
            text.Append($"{edge.SourceId} --> {edge.TargetId}");
            if (edge.Label is not null)
            {
                text.Append(" : ").Append(edge.Label);
            }
            text.Append('\n');
        }
        foreach (var shape in diagram.Shapes)
        {
            var approximated = shape.Kind != ShapeKind.Group && ImportedKind(Keyword(shape.Kind)) != shape.Kind;
            text.Append("' ").Append(LayoutComment.Format(shape, withKind: approximated)).Append('\n');
        }
        // PlantUML can only make all lines orthogonal; with mixed routing the angled edges are listed, as are fixed diamond corners.
        foreach (var edge in diagram.Edges.Where(e => LayoutComment.Needed(e, allAngled)))
        {
            text.Append("' ").Append(LayoutComment.Format(edge)).Append('\n');
        }
        text.Append("@enduml\n");
        return text.ToString();
    }

    public static ParsedDiagram Parse(string text)
    {
        var builder = new ParsedDiagramBuilder(text, laterGroupsAdopt: false);
        var lines = text.Split('\n');
        var started = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var number = i + 1;
            if (line.Length == 0)
            {
                continue;
            }
            if (line.StartsWith('\''))
            {
                if (started)
                {
                    LayoutComment.Read(line[1..].Trim(), number, builder);
                }
                continue;
            }
            if (line.StartsWith("@startuml", StringComparison.Ordinal))
            {
                started = true;
                continue;
            }
            if (!started)
            {
                throw ParsedDiagramBuilder.Error(number, "expected @startuml.");
            }
            if (line.StartsWith("@enduml", StringComparison.Ordinal))
            {
                return builder.Build();
            }
            if (OrthoPattern().IsMatch(line))
            {
                builder.DefaultRouting = EdgeRouting.Angled;
                continue;
            }
            if (IgnoredPattern().IsMatch(line))
            {
                continue;
            }

            var match = TitlePattern().Match(line);
            if (match.Success)
            {
                builder.Title = match.Groups["title"].Value.Trim();
                continue;
            }

            if (line == "}")
            {
                builder.EndGroup(number, "}");
                continue;
            }

            match = GroupPattern().Match(line);
            // An empty "rectangle X {}" stays a plain rectangle.
            if (match.Success && !(match.Groups["close"].Success && match.Groups["keyword"].Value == "rectangle"))
            {
                builder.BeginGroup(
                    number,
                    match.Groups["id"].Success ? match.Groups["id"].Value : null,
                    match.Groups["label"].Success ? match.Groups["label"].Value : null);
                if (match.Groups["close"].Success)
                {
                    builder.EndGroup(number, "}");
                }
                continue;
            }

            match = ElementPattern().Match(line);
            if (match.Success)
            {
                if (match.Groups["body"].Success && !match.Groups["close"].Success)
                {
                    throw ParsedDiagramBuilder.Error(number, "class members are not supported yet.");
                }
                builder.DeclareShape(
                    match.Groups["id"].Value,
                    match.Groups["label"].Success ? match.Groups["label"].Value : null,
                    ImportedKind(match.Groups["keyword"].Value));
                continue;
            }

            match = EdgePattern().Match(line);
            if (match.Success)
            {
                var reversed = match.Groups["arrow"].Value.StartsWith('<');
                var (source, target) = reversed
                    ? (match.Groups["right"].Value, match.Groups["left"].Value)
                    : (match.Groups["left"].Value, match.Groups["right"].Value);
                builder.AddEdge(number, source, target, match.Groups["label"].Success ? match.Groups["label"].Value : null);
                continue;
            }

            throw ParsedDiagramBuilder.Error(number, $"unsupported syntax '{line}'.");
        }

        throw new DiagramValidationException(started ? "Missing @enduml." : "PlantUML text must start with @startuml.");
    }

    [GeneratedRegex(@"^(skinparam\b|hide\b|show\b|allowmixing$|left to right direction$|top to bottom direction$)")]
    private static partial Regex IgnoredPattern();

    [GeneratedRegex(@"^skinparam\s+linetype\s+ortho$", RegexOptions.IgnoreCase)]
    private static partial Regex OrthoPattern();

    [GeneratedRegex(@"^title\s+(?<title>.+)$")]
    private static partial Regex TitlePattern();

    // A container with an opening brace: package "Label" as id {, package id {, package "Label" {, package com.example {.
    [GeneratedRegex(@"^(?<keyword>package|namespace|rectangle|frame|folder|node|cloud|component)\s+(?:""(?<label>[^""]+)""\s+as\s+(?<id>\w+)|(?<id>\w+)\s+as\s+""(?<label>[^""]+)""|(?<id>\w+)|""(?<label>[^""]+)""|(?<label>[\w.]+))\s*(?:<<[^>]*>>\s*)?\{\s*(?<close>\})?$")]
    private static partial Regex GroupPattern();

    [GeneratedRegex(@"^(?<keyword>abstract\s+class|abstract|class|interface|enum|rectangle|database|hexagon|diamond|<>|circle|usecase|storage|file)\s+(?:""(?<label>[^""]+)""\s+as\s+(?<id>\w+)|(?<id>\w+)\s+as\s+""(?<label>[^""]+)""|(?<id>\w+))\s*(?<body>\{\s*(?<close>\})?)?$")]
    private static partial Regex ElementPattern();

    [GeneratedRegex(@"^(?<left>\w+)\s*(?<arrow><-+|<\.+|-+>|\.+>)\s*(?<right>\w+)\s*(?::\s*(?<label>.*))?$")]
    private static partial Regex EdgePattern();
}
