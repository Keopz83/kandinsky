using System.Text;
using System.Text.RegularExpressions;
using Kandinsky.Server.Model;

namespace Kandinsky.Server.Formats;

/// <summary>Mermaid <c>%%</c> comments: Kandinsky layout comments, and init directives that draw all links as steps (angled edges).</summary>
internal static partial class MermaidComments
{
    private const string CommentStart = "%%";
    private const string DirectiveStart = "%%{";
    private const string Indent = "  ";

    public static bool TryRead(string line, int number, ParsedDiagramBuilder builder)
    {
        if (!line.StartsWith(CommentStart, StringComparison.Ordinal))
        {
            return false;
        }
        if (!line.StartsWith(DirectiveStart, StringComparison.Ordinal))
        {
            LayoutComment.Read(line[CommentStart.Length..].Trim(), number, builder);
        }
        else if (IsStepCurve(line))
        {
            builder.DefaultRouting = EdgeRouting.Angled;
        }
        return true;
    }

    /// <summary>Whether the line sets step curves, in an init directive or in the front matter config.</summary>
    public static bool IsStepCurve(string line) => StepCurvePattern().IsMatch(line);

    public static void AppendStepCurveDirective(StringBuilder text) =>
        text.Append("%%{init: {\"flowchart\": {\"curve\": \"step\"}}}%%\n");

    public static void AppendShapeLayouts(StringBuilder text, Diagram diagram)
    {
        foreach (var shape in diagram.Shapes)
        {
            AppendComment(text, LayoutComment.Format(shape));
        }
    }

    public static void AppendEdgeLayouts(StringBuilder text, Diagram diagram, bool allAngled)
    {
        foreach (var edge in diagram.Edges.Where(e => LayoutComment.Needed(e, allAngled)))
        {
            AppendComment(text, LayoutComment.Format(edge));
        }
    }

    private static void AppendComment(StringBuilder text, string comment) =>
        text.Append(Indent).Append(CommentStart).Append(' ').Append(comment).Append('\n');

    // "curve": "step" in an init directive or curve: step in the front matter config; also stepBefore / stepAfter.
    [GeneratedRegex(@"[""']?curve[""']?\s*:\s*[""']?step", RegexOptions.IgnoreCase)]
    private static partial Regex StepCurvePattern();
}
