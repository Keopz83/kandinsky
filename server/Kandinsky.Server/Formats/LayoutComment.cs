using System.Globalization;
using System.Text.RegularExpressions;
using Kandinsky.Server.Model;

namespace Kandinsky.Server.Formats;

/// <summary>
/// Shape positions are stored in format comments (<c>kandinsky:layout s1 x=40 y=40 width=140 height=60</c>),
/// because neither PlantUML nor Mermaid can express them. Other tools ignore these comments.
/// </summary>
internal static partial class LayoutComment
{
    private const string Keyword = "kandinsky:layout";

    public static string Format(Shape shape) =>
        string.Create(CultureInfo.InvariantCulture, $"{Keyword} {shape.Id} x={shape.X} y={shape.Y} width={shape.Width} height={shape.Height}");

    /// <summary>Reads the text of a comment; other comments are ignored.</summary>
    public static void Read(string comment, int line, ParsedDiagramBuilder builder)
    {
        if (!comment.StartsWith(Keyword, StringComparison.Ordinal))
        {
            return;
        }
        var match = Pattern().Match(comment);
        if (!match.Success)
        {
            throw ParsedDiagramBuilder.Error(line, $"invalid layout comment, expected '{Keyword} <id> x=<x> y=<y> width=<width> height=<height>'.");
        }
        double Value(string name) => double.Parse(match.Groups[name].Value, CultureInfo.InvariantCulture);
        builder.SetLayout(line, match.Groups["id"].Value, new ParsedBounds(Value("x"), Value("y"), Value("width"), Value("height")));
    }

    [GeneratedRegex(@"^kandinsky:layout\s+(?<id>\w+)\s+x=(?<x>-?\d+(?:\.\d+)?)\s+y=(?<y>-?\d+(?:\.\d+)?)\s+width=(?<width>\d+(?:\.\d+)?)\s+height=(?<height>\d+(?:\.\d+)?)$")]
    private static partial Regex Pattern();
}
