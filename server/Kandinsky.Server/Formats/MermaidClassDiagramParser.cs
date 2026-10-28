using System.Text.RegularExpressions;

namespace Kandinsky.Server.Formats;

/// <summary>Reads the body of a Mermaid class diagram: classes, namespaces (as groups) and directed associations.</summary>
internal static partial class MermaidClassDiagramParser
{
    private const string NamespaceEnd = "}";

    /// <summary>Reads the lines from <paramref name="start"/>, the line after the <c>classDiagram</c> header.</summary>
    public static ParsedDiagram Parse(string[] lines, int start, ParsedDiagramBuilder builder)
    {
        for (var i = start; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var number = i + 1;
            if (line.Length == 0 || MermaidComments.TryRead(line, number, builder))
            {
                continue;
            }
            ReadStatement(line, number, builder);
        }
        return builder.Build();
    }

    private static void ReadStatement(string line, int number, ParsedDiagramBuilder builder)
    {
        var understood = DirectionPattern().IsMatch(line)
            || TryReadNamespaceEnd(line, number, builder)
            || TryReadNamespace(line, number, builder)
            || TryReadClass(line, number, builder)
            || AssociationStatement.TryRead(line, number, builder);
        if (!understood)
        {
            throw ParsedDiagramBuilder.Error(number, $"unsupported syntax '{line}'.");
        }
    }

    private static bool TryReadNamespaceEnd(string line, int number, ParsedDiagramBuilder builder)
    {
        if (line != NamespaceEnd)
        {
            return false;
        }
        builder.EndGroup(number, NamespaceEnd);
        return true;
    }

    private static bool TryReadNamespace(string line, int number, ParsedDiagramBuilder builder)
    {
        var match = NamespacePattern().Match(line);
        if (match.Success)
        {
            builder.BeginGroup(number, match.Groups["id"].Value, null);
        }
        return match.Success;
    }

    private static bool TryReadClass(string line, int number, ParsedDiagramBuilder builder)
    {
        var match = ClassPattern().Match(line);
        if (!match.Success)
        {
            return false;
        }
        if (match.Groups["body"].Success && !match.Groups["close"].Success)
        {
            throw ParsedDiagramBuilder.Error(number, "class members are not supported yet.");
        }
        builder.DeclareShape(match.Groups["id"].Value, match.Groups["label"].Success ? match.Groups["label"].Value : null);
        return true;
    }

    [GeneratedRegex(@"^direction\s+(TB|BT|LR|RL)$")]
    private static partial Regex DirectionPattern();

    [GeneratedRegex(@"^class\s+(?<id>\w+)(?:\[""(?<label>[^""]+)""\])?\s*(?<body>\{\s*(?<close>\})?)?$")]
    private static partial Regex ClassPattern();

    [GeneratedRegex(@"^namespace\s+(?<id>\w+)\s*\{$")]
    private static partial Regex NamespacePattern();
}
