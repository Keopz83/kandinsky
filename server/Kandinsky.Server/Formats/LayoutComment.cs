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
/// <c>kandinsky:edge A B routing=angled</c> comments carry the routing of edges the format cannot express.
/// </summary>
internal static partial class LayoutComment
{
    private const string Keyword = "kandinsky:layout";
    private const string EdgeKeyword = "kandinsky:edge";

    private static readonly Dictionary<ShapeKind, string> KindNames = Names<ShapeKind>();
    private static readonly Dictionary<string, ShapeKind> Kinds = KindNames.ToDictionary(p => p.Value, p => p.Key);
    private static readonly Dictionary<EdgeRouting, string> RoutingNames = Names<EdgeRouting>();
    private static readonly Dictionary<string, EdgeRouting> Routings = RoutingNames.ToDictionary(p => p.Value, p => p.Key);

    public static string Format(Shape shape, bool withKind = false) =>
        string.Create(CultureInfo.InvariantCulture, $"{Keyword} {shape.Id} x={shape.X} y={shape.Y} width={shape.Width} height={shape.Height}")
        + (withKind ? $" kind={KindNames[shape.Kind]}" : "");

    public static string Format(Edge edge) => $"{EdgeKeyword} {edge.SourceId} {edge.TargetId} routing={RoutingNames[edge.Routing]}";

    /// <summary>Reads the text of a comment; other comments are ignored.</summary>
    public static void Read(string comment, int line, ParsedDiagramBuilder builder)
    {
        if (comment.StartsWith(EdgeKeyword, StringComparison.Ordinal))
        {
            var edge = EdgePattern().Match(comment);
            if (!edge.Success || !Routings.TryGetValue(edge.Groups["routing"].Value, out var routing))
            {
                throw ParsedDiagramBuilder.Error(line, $"invalid edge comment, expected '{EdgeKeyword} <source> <target> routing=straight|angled'.");
            }
            builder.SetEdgeRouting(line, edge.Groups["source"].Value, edge.Groups["target"].Value, routing);
            return;
        }
        if (!comment.StartsWith(Keyword, StringComparison.Ordinal))
        {
            return;
        }
        var match = Pattern().Match(comment);
        if (!match.Success)
        {
            throw ParsedDiagramBuilder.Error(line, $"invalid layout comment, expected '{Keyword} <id> x=<x> y=<y> width=<width> height=<height>'.");
        }
        ShapeKind? kind = null;
        if (match.Groups["kind"].Success)
        {
            kind = Kinds.TryGetValue(match.Groups["kind"].Value, out var k)
                ? k
                : throw ParsedDiagramBuilder.Error(line, $"unknown shape kind '{match.Groups["kind"].Value}'.");
        }
        double Value(string name) => double.Parse(match.Groups[name].Value, CultureInfo.InvariantCulture);
        builder.SetLayout(line, match.Groups["id"].Value, new ParsedBounds(Value("x"), Value("y"), Value("width"), Value("height")), kind);
    }

    private static Dictionary<T, string> Names<T>() where T : struct, Enum =>
        Enum.GetValues<T>().ToDictionary(v => v, v => typeof(T).GetField(v.ToString())!.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()!.Name);

    [GeneratedRegex(@"^kandinsky:layout\s+(?<id>\w+)\s+x=(?<x>-?\d+(?:\.\d+)?)\s+y=(?<y>-?\d+(?:\.\d+)?)\s+width=(?<width>\d+(?:\.\d+)?)\s+height=(?<height>\d+(?:\.\d+)?)(?:\s+kind=(?<kind>[\w-]+))?$")]
    private static partial Regex Pattern();

    [GeneratedRegex(@"^kandinsky:edge\s+(?<source>\w+)\s+(?<target>\w+)\s+routing=(?<routing>\w+)$")]
    private static partial Regex EdgePattern();
}
