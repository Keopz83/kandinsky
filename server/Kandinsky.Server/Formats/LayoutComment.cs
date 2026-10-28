using System.Globalization;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Kandinsky.Server.Model;

namespace Kandinsky.Server.Formats;

/// <summary>
/// Shape positions are stored in format comments (<c>kandinsky:layout s1 x=40 y=40 width=140 height=60</c>),
/// because neither PlantUML nor Mermaid can express them. Other tools ignore these comments.
/// The comment also carries the shape kind (<c>kind=end</c>) when the format can only approximate it, and
/// <c>kandinsky:edge A B routing=angled</c> comments carry the routing of edges the format cannot express,
/// and the diamond corners an edge is attached to (<c>from=bottom to=left</c>) when they are fixed.
/// </summary>
internal static partial class LayoutComment
{
    private const string ShapeKeyword = "kandinsky:layout";
    private const string EdgeKeyword = "kandinsky:edge";

    private static readonly Dictionary<ShapeKind, string> KindNames = JsonNames<ShapeKind>();
    private static readonly Dictionary<string, ShapeKind> KindsByName = KindNames.ToDictionary(p => p.Value, p => p.Key);
    private static readonly Dictionary<EdgeRouting, string> RoutingNames = JsonNames<EdgeRouting>();
    private static readonly Dictionary<string, EdgeRouting> RoutingsByName = RoutingNames.ToDictionary(p => p.Value, p => p.Key);
    private static readonly Dictionary<EdgeCorner, string> CornerNames = JsonNames<EdgeCorner>();
    private static readonly Dictionary<string, EdgeCorner> CornersByName = CornerNames.ToDictionary(p => p.Value, p => p.Key);

    public static string Format(Shape shape, bool withKind = false) =>
        string.Create(CultureInfo.InvariantCulture, $"{ShapeKeyword} {shape.Id} x={shape.X} y={shape.Y} width={shape.Width} height={shape.Height}")
        + (withKind ? $" kind={KindNames[shape.Kind]}" : "");

    public static string Format(Edge edge) =>
        $"{EdgeKeyword} {edge.SourceId} {edge.TargetId} routing={RoutingNames[edge.Routing]}"
        + (edge.SourceCorner == EdgeCorner.Auto ? "" : $" from={CornerNames[edge.SourceCorner]}")
        + (edge.TargetCorner == EdgeCorner.Auto ? "" : $" to={CornerNames[edge.TargetCorner]}");

    /// <summary>Does the edge need an edge comment: angled among other routings, or attached to fixed diamond corners?</summary>
    public static bool Needed(Edge edge, bool allAngled) =>
        (!allAngled && edge.Routing == EdgeRouting.Angled) || edge.HasFixedCorner();

    /// <summary>Reads the text of a comment; other comments are ignored.</summary>
    public static void Read(string comment, int line, ParsedDiagramBuilder builder)
    {
        if (comment.StartsWith(EdgeKeyword, StringComparison.Ordinal))
        {
            ReadEdgeComment(comment, line, builder);
        }
        else if (comment.StartsWith(ShapeKeyword, StringComparison.Ordinal))
        {
            ReadShapeComment(comment, line, builder);
        }
    }

    private static void ReadEdgeComment(string comment, int line, ParsedDiagramBuilder builder)
    {
        var match = EdgePattern().Match(comment);
        if (!match.Success || !RoutingsByName.TryGetValue(match.Groups["routing"].Value, out var routing))
        {
            throw ParsedDiagramBuilder.Error(line, $"invalid edge comment, expected '{EdgeKeyword} <source> <target> routing=straight|angled'.");
        }
        builder.SetEdgeRouting(
            line, match.Groups["source"].Value, match.Groups["target"].Value, routing,
            ParseCorner(match.Groups["from"], line), ParseCorner(match.Groups["to"], line));
    }

    private static void ReadShapeComment(string comment, int line, ParsedDiagramBuilder builder)
    {
        var match = ShapePattern().Match(comment);
        if (!match.Success)
        {
            throw ParsedDiagramBuilder.Error(line, $"invalid layout comment, expected '{ShapeKeyword} <id> x=<x> y=<y> width=<width> height=<height>'.");
        }
        var kind = ParseKind(match.Groups["kind"], line);
        double Number(string name) => double.Parse(match.Groups[name].Value, CultureInfo.InvariantCulture);
        var bounds = new ParsedBounds(Number("x"), Number("y"), Number("width"), Number("height"));
        builder.SetLayout(line, match.Groups["id"].Value, bounds, kind);
    }

    private static EdgeCorner ParseCorner(Group corner, int line)
    {
        if (!corner.Success)
        {
            return EdgeCorner.Auto;
        }
        return CornersByName.TryGetValue(corner.Value, out var value)
            ? value
            : throw ParsedDiagramBuilder.Error(line, $"unknown edge corner '{corner.Value}', expected top, right, bottom, left or auto.");
    }

    private static ShapeKind? ParseKind(Group kind, int line)
    {
        if (!kind.Success)
        {
            return null;
        }
        return KindsByName.TryGetValue(kind.Value, out var value)
            ? value
            : throw ParsedDiagramBuilder.Error(line, $"unknown shape kind '{kind.Value}'.");
    }

    // The names in comments are the JSON names of the API, so that both stay in sync.
    private static Dictionary<T, string> JsonNames<T>() where T : struct, Enum =>
        Enum.GetValues<T>().ToDictionary(v => v, v => typeof(T).GetField(v.ToString())!.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()!.Name);

    [GeneratedRegex(@"^kandinsky:layout\s+(?<id>\w+)\s+x=(?<x>-?\d+(?:\.\d+)?)\s+y=(?<y>-?\d+(?:\.\d+)?)\s+width=(?<width>\d+(?:\.\d+)?)\s+height=(?<height>\d+(?:\.\d+)?)(?:\s+kind=(?<kind>[\w-]+))?$")]
    private static partial Regex ShapePattern();

    [GeneratedRegex(@"^kandinsky:edge\s+(?<source>\w+)\s+(?<target>\w+)\s+routing=(?<routing>\w+)(?:\s+from=(?<from>\w+))?(?:\s+to=(?<to>\w+))?$")]
    private static partial Regex EdgePattern();
}
