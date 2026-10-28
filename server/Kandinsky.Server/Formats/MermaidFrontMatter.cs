using System.Text.RegularExpressions;
using Kandinsky.Server.Diagrams;
using Kandinsky.Server.Model;

namespace Kandinsky.Server.Formats;

/// <summary>The optional YAML front matter between <c>---</c> lines before a Mermaid diagram; only the title and step curves are read.</summary>
internal static partial class MermaidFrontMatter
{
    private const string Delimiter = "---";

    /// <summary>Reads the front matter, if any, and returns the index of the first line after it.</summary>
    public static int Read(string[] lines, ParsedDiagramBuilder builder)
    {
        var start = Array.FindIndex(lines, l => l.Trim().Length > 0);
        if (start < 0 || lines[start].Trim() != Delimiter)
        {
            return 0;
        }

        for (var i = start + 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line == Delimiter)
            {
                return i + 1;
            }
            ReadSetting(line, builder);
        }
        throw new DiagramValidationException("Unterminated front matter: missing closing '---'.");
    }

    private static void ReadSetting(string line, ParsedDiagramBuilder builder)
    {
        var title = TitlePattern().Match(line);
        if (title.Success)
        {
            builder.Title = title.Groups["title"].Value.Trim().Trim('"');
        }
        if (MermaidComments.IsStepCurve(line))
        {
            builder.DefaultRouting = EdgeRouting.Angled;
        }
    }

    [GeneratedRegex(@"^title:\s*(?<title>.+)$")]
    private static partial Regex TitlePattern();
}
