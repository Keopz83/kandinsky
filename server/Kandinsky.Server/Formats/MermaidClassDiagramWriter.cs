using System.Text;
using Kandinsky.Server.Model;

namespace Kandinsky.Server.Formats;

internal static class MermaidClassDiagramWriter
{
    private const string Indent = "  ";

    public static string Write(Diagram diagram)
    {
        var text = new StringBuilder();
        text.Append("classDiagram\n");
        foreach (var shape in diagram.Shapes)
        {
            text.Append($"{Indent}class {shape.Id}[\"{MermaidShapeSyntax.QuotableLabel(shape.Label)}\"]\n");
        }
        foreach (var edge in diagram.Edges)
        {
            text.Append($"{Indent}{edge.SourceId} --> {edge.TargetId}");
            if (edge.Label is not null)
            {
                text.Append(" : ").Append(edge.Label);
            }
            text.Append('\n');
        }
        MermaidComments.AppendShapeLayouts(text, diagram);
        return text.ToString();
    }
}
