using System.Text;
using System.Text.RegularExpressions;
using Kandinsky.Server.Diagrams;
using Kandinsky.Server.Model;

namespace Kandinsky.Server.Formats;

/// <summary>PlantUML class diagram subset: classes (and similar elements) and directed associations.</summary>
public static partial class PlantUml
{
    public static string Export(Diagram diagram)
    {
        var text = new StringBuilder();
        text.Append("@startuml\n");
        text.Append("title ").Append(diagram.Name).Append('\n');
        foreach (var shape in diagram.Shapes)
        {
            text.Append($"class \"{shape.Label.Replace('"', '\'')}\" as {shape.Id}\n");
        }
        foreach (var edge in diagram.Edges)
        {
            text.Append($"{edge.SourceId} --> {edge.TargetId}");
            if (edge.Label is not null)
            {
                text.Append(" : ").Append(edge.Label);
            }
            text.Append('\n');
        }
        text.Append("@enduml\n");
        return text.ToString();
    }

    public static ParsedDiagram Parse(string text)
    {
        var builder = new ParsedDiagramBuilder();
        var lines = text.Split('\n');
        var started = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var number = i + 1;
            if (line.Length == 0 || line.StartsWith('\''))
            {
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

            match = ElementPattern().Match(line);
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

        throw new DiagramValidationException(started ? "Missing @enduml." : "PlantUML text must start with @startuml.");
    }

    [GeneratedRegex(@"^(skinparam\b|hide\b|show\b|left to right direction$|top to bottom direction$)")]
    private static partial Regex IgnoredPattern();

    [GeneratedRegex(@"^title\s+(?<title>.+)$")]
    private static partial Regex TitlePattern();

    [GeneratedRegex(@"^(?:abstract\s+class|abstract|class|interface|enum|rectangle)\s+(?:""(?<label>[^""]+)""\s+as\s+(?<id>\w+)|(?<id>\w+)\s+as\s+""(?<label>[^""]+)""|(?<id>\w+))\s*(?<body>\{\s*(?<close>\})?)?$")]
    private static partial Regex ElementPattern();

    [GeneratedRegex(@"^(?<left>\w+)\s*(?<arrow><-+|<\.+|-+>|\.+>)\s*(?<right>\w+)\s*(?::\s*(?<label>.*))?$")]
    private static partial Regex EdgePattern();
}
