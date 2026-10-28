using System.Text.RegularExpressions;
using Kandinsky.Server.Diagrams;

namespace Kandinsky.Server.Formats;

public enum DiagramFormat
{
    PlantUml,
    Mermaid,
}

public static partial class DiagramFormats
{
    public static DiagramFormat Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "plantuml" or "puml" => DiagramFormat.PlantUml,
        "mermaid" or "mmd" => DiagramFormat.Mermaid,
        _ => throw new DiagramValidationException($"Unknown format '{value}'. Use 'plantuml' or 'mermaid'."),
    };

    public static DiagramFormat Detect(string text)
    {
        var firstLine = text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0 && !l.StartsWith('\''));
        return firstLine switch
        {
            not null when firstLine.StartsWith("@startuml", StringComparison.Ordinal) => DiagramFormat.PlantUml,
            not null when firstLine == "---" || firstLine.StartsWith("%%") || MermaidHeaderPattern().IsMatch(firstLine) => DiagramFormat.Mermaid,
            _ => throw new DiagramValidationException(
                $"Cannot detect the format of '{(firstLine is null ? "" : firstLine.Length > 40 ? firstLine[..40] + "…" : firstLine)}'. Specify 'plantuml' or 'mermaid'."),
        };
    }

    /// <summary>
    /// Text copied from browsers, chat or wiki pages may contain other line breaks, no-break spaces or invisible characters
    /// (BOM, zero-width spaces); they are turned into plain line breaks and spaces or removed.
    /// </summary>
    public static string Normalize(string text) => InvisiblePattern().Replace(
        SpacePattern().Replace(LineBreakPattern().Replace(text, "\n"), " "),
        "");

    /// <summary>Blanks the lines of a Markdown code fence around the text (e.g. <c>```mermaid</c>), keeping line numbers.</summary>
    public static string Unfence(string text)
    {
        var lines = text.Split('\n');
        var first = Array.FindIndex(lines, l => l.Trim().Length > 0);
        var opening = first < 0 ? Match.Empty : FencePattern().Match(lines[first].Trim());
        if (!opening.Success)
        {
            return text;
        }
        var fence = opening.Groups["fence"].Value;
        var last = Array.FindLastIndex(lines, l => l.Trim().Length > 0);
        var closing = lines[last].Trim();
        if (last == first || !closing.StartsWith(fence, StringComparison.Ordinal) || closing.Any(c => c != fence[0]))
        {
            throw new DiagramValidationException($"Line {first + 1}: code fence is not closed.");
        }
        lines[first] = lines[last] = "";
        return string.Join('\n', lines);
    }

    public static string Export(Model.Diagram diagram, DiagramFormat format) => format switch
    {
        DiagramFormat.PlantUml => PlantUml.Export(diagram),
        _ => Mermaid.Export(diagram),
    };

    public static ParsedDiagram Parse(string text, DiagramFormat format) => format switch
    {
        DiagramFormat.PlantUml => PlantUml.Parse(text),
        _ => Mermaid.Parse(text),
    };

    // Every Mermaid diagram type, so that unsupported ones get a specific error from the Mermaid parser.
    [GeneratedRegex(@"^(?:classDiagram|flowchart|graph|sequenceDiagram|stateDiagram|erDiagram|gantt|pie|journey|gitGraph|mindmap|timeline|quadrantChart|requirementDiagram|C4\w+|kanban|\w+-beta)\b")]
    private static partial Regex MermaidHeaderPattern();

    [GeneratedRegex(@"^(?<fence>`{3,}|~{3,})\s*[\w-]*$")]
    private static partial Regex FencePattern();

    [GeneratedRegex(@"\r\n?|[\u0085\u2028\u2029]")]
    private static partial Regex LineBreakPattern();

    [GeneratedRegex(@"[\u00A0\u2007\u202F\t]")]
    private static partial Regex SpacePattern();

    [GeneratedRegex(@"[\uFEFF\u200B-\u200D\u2060\u00AD]")]
    private static partial Regex InvisiblePattern();
}
