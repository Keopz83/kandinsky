using Kandinsky.Server.Diagrams;

namespace Kandinsky.Server.Formats;

public enum DiagramFormat
{
    PlantUml,
    Mermaid,
}

public static class DiagramFormats
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
            not null when firstLine == "---" || firstLine.StartsWith("%%") || firstLine.StartsWith("classDiagram", StringComparison.Ordinal) => DiagramFormat.Mermaid,
            _ => throw new DiagramValidationException("Cannot detect the format. Specify 'plantuml' or 'mermaid'."),
        };
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
}
