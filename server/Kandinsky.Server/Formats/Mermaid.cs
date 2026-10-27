using System.Text;
using System.Text.RegularExpressions;
using Kandinsky.Server.Diagrams;
using Kandinsky.Server.Model;

namespace Kandinsky.Server.Formats;

/// <summary>Mermaid classDiagram subset: classes with optional labels and directed associations.</summary>
public static partial class Mermaid
{
    public static string Export(Diagram diagram)
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
        foreach (var shape in diagram.Shapes)
        {
            text.Append("  %% ").Append(LayoutComment.Format(shape)).Append('\n');
        }
        return text.ToString();
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
                if (line is not ("classDiagram" or "classDiagram-v2"))
                {
                    throw ParsedDiagramBuilder.Error(number, "expected 'classDiagram'. Only Mermaid class diagrams are supported.");
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
            throw new DiagramValidationException("Mermaid text must contain 'classDiagram'.");
        }
        return builder.Build();
    }

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
}
