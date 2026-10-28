using System.Text;
using Kandinsky.Server.Model;

namespace Kandinsky.Server.Formats;

internal static class MermaidFlowchartWriter
{
    private const string Indent = "  ";

    public static string Write(Diagram diagram)
    {
        var text = new StringBuilder();
        // Mermaid can only draw all links as steps; with mixed routing the angled edges are listed in comments, as are fixed diamond corners.
        var allAngled = diagram.HasOnlyAngledEdges();
        if (allAngled)
        {
            MermaidComments.AppendStepCurveDirective(text);
        }
        text.Append("flowchart TB\n");
        var tree = new ShapeTree(diagram.Shapes);
        AppendNodes(text, tree, tree.TopLevel, Indent);
        AppendLinks(text, diagram.Edges);
        MermaidComments.AppendShapeLayouts(text, diagram);
        MermaidComments.AppendEdgeLayouts(text, diagram, allAngled);
        return text.ToString();
    }

    private static void AppendNodes(StringBuilder text, ShapeTree tree, IEnumerable<Shape> shapes, string indent)
    {
        foreach (var shape in shapes)
        {
            var label = MermaidShapeSyntax.QuotableLabel(shape.Label);
            if (shape.Kind == ShapeKind.Group)
            {
                text.Append($"{indent}subgraph {shape.Id} [\"{label}\"]\n");
                AppendNodes(text, tree, tree.ChildrenOf(shape), indent + Indent);
                text.Append($"{indent}end\n");
                continue;
            }
            text.Append(MermaidShapeSyntax.ClassicBrackets(shape.Kind) is (var open, var close)
                ? $"{indent}{shape.Id}{open}\"{label}\"{close}\n"
                : $"{indent}{shape.Id}@{{ shape: {MermaidShapeSyntax.V11ShapeName(shape.Kind)}, label: \"{label}\" }}\n");
        }
    }

    private static void AppendLinks(StringBuilder text, IEnumerable<Edge> edges)
    {
        foreach (var edge in edges)
        {
            text.Append($"{Indent}{edge.SourceId} -->");
            if (edge.Label is not null)
            {
                text.Append($"|\"{MermaidShapeSyntax.QuotableLabel(edge.Label)}\"|");
            }
            text.Append($" {edge.TargetId}\n");
        }
    }
}
