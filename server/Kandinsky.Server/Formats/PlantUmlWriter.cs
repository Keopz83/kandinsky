using System.Text;
using Kandinsky.Server.Model;

namespace Kandinsky.Server.Formats;

internal static class PlantUmlWriter
{
    private const string CommentPrefix = "' ";
    private const string Indent = "  ";

    public static string Write(Diagram diagram)
    {
        var text = new StringBuilder();
        text.Append("@startuml\n");
        AppendSettings(text, diagram);
        var tree = new ShapeTree(diagram.Shapes);
        AppendShapes(text, tree, tree.TopLevel, "");
        AppendEdges(text, diagram.Edges);
        AppendLayoutComments(text, diagram);
        text.Append("@enduml\n");
        return text.ToString();
    }

    private static void AppendSettings(StringBuilder text, Diagram diagram)
    {
        // Class diagrams accept deployment elements such as database or hexagon only with mixing allowed.
        if (diagram.Shapes.Any(s => s.Kind is not (ShapeKind.Rectangle or ShapeKind.Group)))
        {
            text.Append("allowmixing\n");
        }
        if (diagram.HasOnlyAngledEdges())
        {
            text.Append("skinparam linetype ortho\n");
        }
    }

    private static void AppendShapes(StringBuilder text, ShapeTree tree, IEnumerable<Shape> shapes, string indent)
    {
        foreach (var shape in shapes)
        {
            text.Append($"{indent}{PlantUmlElements.Keyword(shape.Kind)} \"{QuotableLabel(shape.Label)}\" as {shape.Id}");
            if (shape.Kind == ShapeKind.Group)
            {
                text.Append(" {\n");
                AppendShapes(text, tree, tree.ChildrenOf(shape), indent + Indent);
                text.Append(indent).Append('}');
            }
            text.Append('\n');
        }
    }

    private static void AppendEdges(StringBuilder text, IEnumerable<Edge> edges)
    {
        foreach (var edge in edges)
        {
            text.Append($"{edge.SourceId} --> {edge.TargetId}");
            if (edge.Label is not null)
            {
                text.Append(" : ").Append(edge.Label);
            }
            text.Append('\n');
        }
    }

    private static void AppendLayoutComments(StringBuilder text, Diagram diagram)
    {
        foreach (var shape in diagram.Shapes)
        {
            text.Append(CommentPrefix).Append(LayoutComment.Format(shape, withKind: PlantUmlElements.IsApproximated(shape.Kind))).Append('\n');
        }
        // PlantUML can only make all lines orthogonal; with mixed routing the angled edges are listed, as are fixed diamond corners.
        var allAngled = diagram.HasOnlyAngledEdges();
        foreach (var edge in diagram.Edges.Where(e => LayoutComment.Needed(e, allAngled)))
        {
            text.Append(CommentPrefix).Append(LayoutComment.Format(edge)).Append('\n');
        }
    }

    // Double quotes would end the quoted label.
    private static string QuotableLabel(string label) => label.Replace('"', '\'');
}
