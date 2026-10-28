using System.Text;
using System.Text.RegularExpressions;
using Kandinsky.Server.Diagrams;
using Kandinsky.Server.Model;

namespace Kandinsky.Server.Formats;

/// <summary>
/// Mermaid subset: classDiagram with classes, optional labels and directed associations;
/// flowchart with nodes (database nodes <c>[(text)]</c> become databases, any other shape a rectangle) and directed links.
/// </summary>
public static partial class Mermaid
{
    /// <summary>Exports a class diagram, or a flowchart when the diagram has databases, which class diagrams cannot express.</summary>
    public static string Export(Diagram diagram) =>
        diagram.Shapes.Any(s => s.Kind == ShapeKind.Database) ? ExportFlowchart(diagram) : ExportClassDiagram(diagram);

    private static string ExportFlowchart(Diagram diagram)
    {
        var text = new StringBuilder();
        text.Append("flowchart TB\n");
        foreach (var shape in diagram.Shapes)
        {
            var (open, close) = shape.Kind == ShapeKind.Database ? ("[(", ")]") : ("[", "]");
            text.Append($"  {shape.Id}{open}\"{shape.Label.Replace('"', '\'')}\"{close}\n");
        }
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
        var builder = new ParsedDiagramBuilder();
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
                LayoutComment.Read(line[2..].Trim(), number, builder);
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

            var match = ClassPattern().Match(line);
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
                LayoutComment.Read(line[2..].Trim(), number, builder);
                continue;
            }
            // Styling has no counterpart in Kandinsky.
            if (FlowchartStylePattern().IsMatch(line))
            {
                continue;
            }
            if (line.StartsWith("subgraph", StringComparison.Ordinal))
            {
                throw ParsedDiagramBuilder.Error(number, "subgraphs are not supported yet.");
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
            var open = node.Groups["open"];
            builder.DeclareShape(
                id,
                node.Groups["text"].Success ? FlowchartText(node.Groups["text"].Value) : null,
                !open.Success ? null : open.Value == "[(" ? ShapeKind.Database : ShapeKind.Rectangle);
            ids.Add(id);

            var and = AndPattern().Match(line, position);
            if (!and.Success)
            {
                return ids;
            }
            position += and.Length;
        }
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

    [GeneratedRegex(@"^(?:flowchart|graph)(?:\s+(?:TB|TD|BT|LR|RL))?\s*;?$")]
    private static partial Regex FlowchartPattern();

    [GeneratedRegex(@"^(?:style|classDef|class|linkStyle|click|direction)\s")]
    private static partial Regex FlowchartStylePattern();

    // A node id with an optional shape such as [text], (text), [(text)] (database), {text}, ((text)) or "quoted" text, and an optional :::class.
    [GeneratedRegex(@"\G\s*(?<id>\w+)(?:(?<open>\(\(\(|\(\[|\[\[|\[\(|\(\(|\{\{|\[/|\[\\|\[|\(|\{|>)\s*(?:""(?<text>[^""]*)""|(?<text>[^""]*?))\s*(?:\)\)\)|\]\)|\]\]|\)\]|\)\)|\}\}|/\]|\\\]|\]|\)|\}))?(?::::\w+)?\s*")]
    private static partial Regex NodePattern();

    // -->, -.->, ==> (any length) with an optional |label|, or the text forms -- label -->, -. label .->, == label ==>.
    [GeneratedRegex(@"\G\s*(?:(?:-{2,}>|-\.+->|={2,}>)(?:\s*\|(?:""(?<label>[^""]*)""|(?<label>[^|]*))\|)?|--\s*(?<label>[^>]+?)\s*-{2,}>|-\.\s*(?<label>[^>]+?)\s*\.+->|==\s*(?<label>[^>]+?)\s*={2,}>)\s*")]
    private static partial Regex LinkPattern();

    // Undirected and other links: ---, -.-, ===, ~~~, --o, --x, <-->.
    [GeneratedRegex(@"\G\s*[<ox]?[-=.~]{2,}")]
    private static partial Regex OtherLinkPattern();

    [GeneratedRegex(@"\G\s*&\s*")]
    private static partial Regex AndPattern();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreakPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
