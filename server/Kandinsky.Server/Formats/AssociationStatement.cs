using System.Text.RegularExpressions;

namespace Kandinsky.Server.Formats;

/// <summary>
/// A directed association in PlantUML and Mermaid class diagrams: <c>A --&gt; B : label</c>, also dotted (<c>..&gt;</c>)
/// or reversed (<c>A &lt;-- B</c>, which points from B to A).
/// </summary>
internal static partial class AssociationStatement
{
    public static bool TryRead(string line, int number, ParsedDiagramBuilder builder)
    {
        var match = Pattern().Match(line);
        if (!match.Success)
        {
            return false;
        }
        var reversed = match.Groups["arrow"].Value.StartsWith('<');
        var (left, right) = (match.Groups["left"].Value, match.Groups["right"].Value);
        var (source, target) = reversed ? (right, left) : (left, right);
        builder.AddEdge(number, source, target, match.Groups["label"].Success ? match.Groups["label"].Value : null);
        return true;
    }

    [GeneratedRegex(@"^(?<left>\w+)\s*(?<arrow><-+|<\.+|-+>|\.+>)\s*(?<right>\w+)\s*(?::\s*(?<label>.*))?$")]
    private static partial Regex Pattern();
}
