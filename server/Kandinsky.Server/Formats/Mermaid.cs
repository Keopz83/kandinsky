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
            ? MermaidFlowchartWriter.Write(diagram)
            : MermaidClassDiagramWriter.Write(diagram);

    /// <summary>Reads the front matter and the comments before the diagram header, then the diagram of the type the header names.</summary>
    public static ParsedDiagram Parse(string text)
    {
        var builder = new ParsedDiagramBuilder(text, laterGroupsAdopt: true);
        var lines = text.Split('\n');
        for (var i = MermaidFrontMatter.Read(lines, builder); i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var number = i + 1;
            if (line.Length == 0 || MermaidComments.TryRead(line, number, builder))
            {
                continue;
            }
            if (FlowchartHeaderPattern().IsMatch(line))
            {
                return MermaidFlowchartParser.Parse(lines, i + 1, builder);
            }
            if (ClassDiagramHeaderPattern().IsMatch(line))
            {
                return MermaidClassDiagramParser.Parse(lines, i + 1, builder);
            }
            throw ParsedDiagramBuilder.Error(number, "expected 'classDiagram' or 'flowchart'. Only Mermaid class diagrams and flowcharts are supported.");
        }
        throw new DiagramValidationException("Mermaid text must contain 'classDiagram' or 'flowchart'.");
    }

    [GeneratedRegex(@"^(?:flowchart|graph)(?:\s+(?:TB|TD|BT|LR|RL))?\s*;?$")]
    private static partial Regex FlowchartHeaderPattern();

    [GeneratedRegex(@"^classDiagram(?:-v2)?$")]
    private static partial Regex ClassDiagramHeaderPattern();
}
