using System.Text;
using System.Text.RegularExpressions;
using Kandinsky.Server.Diagrams;
using Kandinsky.Server.Model;

namespace Kandinsky.Server.Formats;

/// <summary>
/// Mermaid subset: classDiagram with classes, namespaces (as groups), optional labels and directed associations;
/// flowchart with nodes in the classic syntax (<c>[(text)]</c>, <c>{text}</c>, <c>([text])</c>, …) or the v11 shape syntax
/// (<c>A@{ shape: doc, label: "text" }</c>), subgraphs (as groups) and directed links.
/// </summary>
public static partial class Mermaid
{
    /// <summary>
    /// Exports a class diagram, or a flowchart when the diagram has shapes other than rectangles or angled edges,
    /// which class diagrams cannot express.
    /// </summary>
    public static string Export(Diagram diagram) =>
        diagram.Shapes.Any(s => s.Kind != ShapeKind.Rectangle) || diagram.Edges.Any(e => e.Routing == EdgeRouting.Angled)
            ? ExportFlowchart(diagram)
            : ExportClassDiagram(diagram);

    /// <summary>The classic node syntax of a kind, or null for kinds that need the v11 shape syntax.</summary>
    private static (string Open, string Close)? ClassicShape(ShapeKind kind) => kind switch
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

    private static string V11Shape(ShapeKind kind) => kind switch
    {
        ShapeKind.Start => "sm-circ",
        ShapeKind.End => "fr-circ",
        ShapeKind.Document => "doc",
        _ => "rect",
    };

    /// <summary>Kinds of the classic node syntax by opening and closing bracket; other combinations are rectangles.</summary>
    private static ShapeKind ClassicKind(string open, string close) => (open, close) switch
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
    private static ShapeKind V11Kind(string shape) => shape switch
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

    private static string ExportFlowchart(Diagram diagram)
    {
        var text = new StringBuilder();
        // Mermaid can only draw all links as steps; with mixed routing the angled edges are listed in comments.
        var allAngled = diagram.Edges.Count > 0 && diagram.Edges.All(e => e.Routing == EdgeRouting.Angled);
        if (allAngled)
        {
            text.Append("%%{init: {\"flowchart\": {\"curve\": \"step\"}}}%%\n");
        }
        text.Append("flowchart TB\n");
        var parents = DiagramGroups.Parents(diagram.Shapes);
        var children = diagram.Shapes.ToLookup(s => parents[s.Id] ?? "");
        void AppendShapes(string parentId, string indent)
        {
            foreach (var shape in children[parentId])
            {
                var label = shape.Label.Replace('"', '\'');
                if (shape.Kind == ShapeKind.Group)
                {
                    text.Append($"{indent}subgraph {shape.Id} [\"{label}\"]\n");
                    AppendShapes(shape.Id, indent + "  ");
                    text.Append($"{indent}end\n");
                    continue;
                }
                text.Append(ClassicShape(shape.Kind) is (var open, var close)
                    ? $"{indent}{shape.Id}{open}\"{label}\"{close}\n"
                    : $"{indent}{shape.Id}@{{ shape: {V11Shape(shape.Kind)}, label: \"{label}\" }}\n");
            }
        }
        AppendShapes("", "  ");
        foreach (var edge in diagram.Edges)
        {
            text.Append($"  {edge.SourceId} -->");
            if (edge.Label is not null)
            {
                text.Append($"|\"{edge.Label.Replace('"', '\'')}\"|");
            }
            text.Append($" {edge.TargetId}\n");
        }
        AppendLayout(text, diagram);
        foreach (var edge in diagram.Edges.Where(e => !allAngled && e.Routing == EdgeRouting.Angled))
        {
            text.Append("  %% ").Append(LayoutComment.Format(edge)).Append('\n');
        }
        return text.ToString();
    }

    private static string ExportClassDiagram(Diagram diagram)
    {
        var text = new StringBuilder();
        text.Append("classDiagram\n");
        foreach (var shape in diagram.Shapes)
        {
            text.Append($"  class {shape.Id}[\"{shape.Label.Replace('"', '\'')}\"]\n");
        }
        foreach (var edge in diagram.Edges)
        {
            text.Append($"  {edge.SourceId} --> {edge.TargetId}");
            if (edge.Label is not null)
            {
                text.Append(" : ").Append(edge.Label);
            }
            text.Append('\n');
        }
        AppendLayout(text, diagram);
        return text.ToString();
    }

    private static void AppendLayout(StringBuilder text, Diagram diagram)
    {
        foreach (var shape in diagram.Shapes)
        {
            text.Append("  %% ").Append(LayoutComment.Format(shape)).Append('\n');
        }
    }

    public static ParsedDiagram Parse(string text)
    {
        var builder = new ParsedDiagramBuilder(text, laterGroupsAdopt: true);
        var lines = text.Split('\n');
        var i = SkipFrontMatter(lines, builder);
        var started = false;

        for (; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var number = i + 1;
            if (line.Length == 0)
            {
                continue;
            }
            if (line.StartsWith("%%", StringComparison.Ordinal))
            {
                ReadComment(line, number, builder);
                continue;
            }
            if (!started)
            {
                if (FlowchartPattern().IsMatch(line))
                {
                    return ParseFlowchart(lines, i + 1, builder);
                }
                if (line is not ("classDiagram" or "classDiagram-v2"))
                {
                    throw ParsedDiagramBuilder.Error(number, "expected 'classDiagram' or 'flowchart'. Only Mermaid class diagrams and flowcharts are supported.");
                }
                started = true;
                continue;
            }
            if (DirectionPattern().IsMatch(line))
            {
                continue;
            }
            if (line == "}")
            {
                builder.EndGroup(number, "}");
                continue;
            }

            var match = NamespacePattern().Match(line);
            if (match.Success)
            {
                builder.BeginGroup(number, match.Groups["id"].Value, null);
                continue;
            }

            match = ClassPattern().Match(line);
            if (match.Success)
            {
                if (match.Groups["body"].Success && !match.Groups["close"].Success)
                {
                    throw ParsedDiagramBuilder.Error(number, "class members are not supported yet.");
                }
                builder.DeclareShape(match.Groups["id"].Value, match.Groups["label"].Success ? match.Groups["label"].Value : null);
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

        if (!started)
        {
            throw new DiagramValidationException("Mermaid text must contain 'classDiagram' or 'flowchart'.");
        }
        return builder.Build();
    }

    private static ParsedDiagram ParseFlowchart(string[] lines, int start, ParsedDiagramBuilder builder)
    {
        for (var i = start; i < lines.Length; i++)
        {
            var line = lines[i].Trim().TrimEnd(';').TrimEnd();
            var number = i + 1;
            if (line.Length == 0)
            {
                continue;
            }
            if (line.StartsWith("%%", StringComparison.Ordinal))
            {
                ReadComment(line, number, builder);
                continue;
            }
            // Styling has no counterpart in Kandinsky.
            if (FlowchartStylePattern().IsMatch(line))
            {
                continue;
            }
            if (line == "end")
            {
                builder.EndGroup(number, "end");
                continue;
            }
            var subgraph = SubgraphPattern().Match(line);
            if (subgraph.Success)
            {
                builder.BeginGroup(
                    number,
                    subgraph.Groups["id"].Success ? subgraph.Groups["id"].Value : null,
                    subgraph.Groups["label"].Success ? FlowchartText(subgraph.Groups["label"].Value) : null);
                continue;
            }
            ReadFlowchartStatement(line, number, builder);
        }
        return builder.Build();
    }

    /// <summary>Reads <c>A[label] --&gt; B &amp; C --&gt;|label| D</c>: a chain of node groups joined by directed links.</summary>
    private static void ReadFlowchartStatement(string line, int number, ParsedDiagramBuilder builder)
    {
        var position = 0;
        var sources = ReadFlowchartNodes(line, ref position, number, builder);
        while (position < line.Length)
        {
            var link = LinkPattern().Match(line, position);
            if (!link.Success)
            {
                throw ParsedDiagramBuilder.Error(number, OtherLinkPattern().IsMatch(line, position)
                    ? "only directed links ('-->', '-.->', '==>') are supported."
                    : $"unsupported syntax '{line}'.");
            }
            position += link.Length;
            var targets = ReadFlowchartNodes(line, ref position, number, builder);
            var label = link.Groups["label"].Success ? FlowchartText(link.Groups["label"].Value) : null;
            foreach (var source in sources)
            {
                foreach (var target in targets)
                {
                    builder.AddEdge(number, source, target, label);
                }
            }
            sources = targets;
        }
    }

    private static List<string> ReadFlowchartNodes(string line, ref int position, int number, ParsedDiagramBuilder builder)
    {
        var ids = new List<string>();
        while (true)
        {
            var node = NodePattern().Match(line, position);
            if (!node.Success)
            {
                throw ParsedDiagramBuilder.Error(number, $"unsupported syntax '{line}'.");
            }
            position += node.Length;
            var id = node.Groups["id"].Value;
            if (node.Groups["props"].Success)
            {
                // v11 shape syntax: A@{ shape: doc, label: "text" }
                var props = node.Groups["props"].Value;
                var shape = ShapePropertyPattern().Match(props);
                var label = LabelPropertyPattern().Match(props);
                builder.DeclareShape(
                    id,
                    label.Success ? FlowchartText(label.Groups["label"].Value) : null,
                    shape.Success ? V11Kind(shape.Groups["shape"].Value) : null);
            }
            else
            {
                var open = node.Groups["open"];
                builder.DeclareShape(
                    id,
                    node.Groups["text"].Success ? FlowchartText(node.Groups["text"].Value) : null,
                    open.Success ? ClassicKind(open.Value, node.Groups["close"].Value) : null);
            }
            ids.Add(id);

            var and = AndPattern().Match(line, position);
            if (!and.Success)
            {
                return ids;
            }
            position += and.Length;
        }
    }

    /// <summary>Reads layout comments, and a directive that draws all links as steps (angled edges).</summary>
    private static void ReadComment(string line, int number, ParsedDiagramBuilder builder)
    {
        if (line.StartsWith("%%{", StringComparison.Ordinal))
        {
            if (StepCurvePattern().IsMatch(line))
            {
                builder.DefaultRouting = EdgeRouting.Angled;
            }
            return;
        }
        LayoutComment.Read(line[2..].Trim(), number, builder);
    }

    // Labels are single-line: line breaks become spaces.
    private static string FlowchartText(string text) =>
        WhitespacePattern().Replace(LineBreakPattern().Replace(text.Trim().Trim('"'), " "), " ").Trim();

    private static int SkipFrontMatter(string[] lines, ParsedDiagramBuilder builder)
    {
        var start = Array.FindIndex(lines, l => l.Trim().Length > 0);
        if (start < 0 || lines[start].Trim() != "---")
        {
            return 0;
        }

        for (var i = start + 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line == "---")
            {
                return i + 1;
            }
            var match = TitlePattern().Match(line);
            if (match.Success)
            {
                builder.Title = match.Groups["title"].Value.Trim().Trim('"');
            }
            if (StepCurvePattern().IsMatch(line))
            {
                builder.DefaultRouting = EdgeRouting.Angled;
            }
        }
        throw new DiagramValidationException("Unterminated front matter: missing closing '---'.");
    }

    [GeneratedRegex(@"^title:\s*(?<title>.+)$")]
    private static partial Regex TitlePattern();

    [GeneratedRegex(@"^direction\s+(TB|BT|LR|RL)$")]
    private static partial Regex DirectionPattern();

    [GeneratedRegex(@"^class\s+(?<id>\w+)(?:\[""(?<label>[^""]+)""\])?\s*(?<body>\{\s*(?<close>\})?)?$")]
    private static partial Regex ClassPattern();

    [GeneratedRegex(@"^(?<left>\w+)\s*(?<arrow><-+|<\.+|-+>|\.+>)\s*(?<right>\w+)\s*(?::\s*(?<label>.*))?$")]
    private static partial Regex EdgePattern();

    [GeneratedRegex(@"^namespace\s+(?<id>\w+)\s*\{$")]
    private static partial Regex NamespacePattern();

    // subgraph id, subgraph id [title], subgraph id["title"], or subgraph followed by a title only (the id is generated).
    [GeneratedRegex(@"^subgraph\s+(?:(?<id>\w+)\s*(?:\[\s*(?:""(?<label>[^""]*)""|(?<label>[^\]""]*?))\s*\])?|""(?<label>[^""]+)""|(?<label>.+?))$")]
    private static partial Regex SubgraphPattern();

    [GeneratedRegex(@"^(?:flowchart|graph)(?:\s+(?:TB|TD|BT|LR|RL))?\s*;?$")]
    private static partial Regex FlowchartPattern();

    [GeneratedRegex(@"^(?:style|classDef|class|linkStyle|click|direction)\s")]
    private static partial Regex FlowchartStylePattern();

    // A node id with an optional shape such as [text], (text), [(text)] (database), {text}, ((text)) or "quoted" text,
    // or the v11 shape syntax @{ shape: …, label: … }, and an optional :::class.
    [GeneratedRegex(@"\G\s*(?<id>\w+)(?:@\{(?<props>[^}]*)\}|(?<open>\(\(\(|\(\[|\[\[|\[\(|\(\(|\{\{|\[/|\[\\|\[|\(|\{|>)\s*(?:""(?<text>[^""]*)""|(?<text>[^""]*?))\s*(?<close>\)\)\)|\]\)|\]\]|\)\]|\)\)|\}\}|/\]|\\\]|\]|\)|\}))?(?::::\w+)?\s*")]
    private static partial Regex NodePattern();

    // -->, -.->, ==> (any length) with an optional |label|, or the text forms -- label -->, -. label .->, == label ==>.
    [GeneratedRegex(@"\G\s*(?:(?:-{2,}>|-\.+->|={2,}>)(?:\s*\|(?:""(?<label>[^""]*)""|(?<label>[^|]*))\|)?|--\s*(?<label>[^>]+?)\s*-{2,}>|-\.\s*(?<label>[^>]+?)\s*\.+->|==\s*(?<label>[^>]+?)\s*={2,}>)\s*")]
    private static partial Regex LinkPattern();

    // Undirected and other links: ---, -.-, ===, ~~~, --o, --x, <-->.
    [GeneratedRegex(@"\G\s*[<ox]?[-=.~]{2,}")]
    private static partial Regex OtherLinkPattern();

    [GeneratedRegex(@"\bshape:\s*(?<shape>[\w-]+)")]
    private static partial Regex ShapePropertyPattern();

    [GeneratedRegex(@"\blabel:\s*(?:""(?<label>[^""]*)""|(?<label>[^,]*))")]
    private static partial Regex LabelPropertyPattern();

    // "curve": "step" in an init directive or curve: step in the front matter config; also stepBefore / stepAfter.
    [GeneratedRegex(@"[""']?curve[""']?\s*:\s*[""']?step", RegexOptions.IgnoreCase)]
    private static partial Regex StepCurvePattern();

    [GeneratedRegex(@"\G\s*&\s*")]
    private static partial Regex AndPattern();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreakPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
