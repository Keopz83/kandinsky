using System.Text.RegularExpressions;
using Kandinsky.Server.Diagrams;
using Kandinsky.Server.Model;

namespace Kandinsky.Server.Formats;

/// <summary>Entry point for the text formats: format names, detection, clean-up of pasted text, export and parsing.</summary>
public static partial class DiagramFormats
{
    private const int MaxExcerptLength = 40;

    public static DiagramFormat Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "plantuml" or "puml" => DiagramFormat.PlantUml,
        "mermaid" or "mmd" => DiagramFormat.Mermaid,
        _ => throw new DiagramValidationException($"Unknown format '{value}'. Use 'plantuml' or 'mermaid'."),
    };

    /// <summary>Detects the format from the first line that is not blank or a PlantUML comment.</summary>
    public static DiagramFormat Detect(string text)
    {
        var firstLine = text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0 && !l.StartsWith('\'')) ?? "";
        if (firstLine.StartsWith("@startuml", StringComparison.Ordinal))
        {
            return DiagramFormat.PlantUml;
        }
        if (firstLine == "---" || firstLine.StartsWith("%%") || MermaidHeaderPattern().IsMatch(firstLine))
        {
            return DiagramFormat.Mermaid;
        }
        throw new DiagramValidationException($"Cannot detect the format of '{Excerpt(firstLine)}'. Specify 'plantuml' or 'mermaid'.");
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

    public static string Export(Diagram diagram, DiagramFormat format) => format switch
    {
        DiagramFormat.PlantUml => PlantUml.Export(diagram),
        DiagramFormat.Mermaid => Mermaid.Export(diagram),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    public static ParsedDiagram Parse(string text, DiagramFormat format) => format switch
    {
        DiagramFormat.PlantUml => PlantUml.Parse(text),
        DiagramFormat.Mermaid => Mermaid.Parse(text),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    private static string Excerpt(string line) => line.Length > MaxExcerptLength ? line[..MaxExcerptLength] + "…" : line;

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
