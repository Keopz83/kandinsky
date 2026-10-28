using System.Text.RegularExpressions;
using Kandinsky.Server.Diagrams;
using Kandinsky.Server.Model;

namespace Kandinsky.Server.Formats;

internal static partial class PlantUmlParser
{
    private const string StartTag = "@startuml";
    private const string EndTag = "@enduml";
    private const char CommentStart = '\'';
    private const string GroupEnd = "}";

    public static ParsedDiagram Parse(string text)
    {
        var builder = new ParsedDiagramBuilder(text, laterGroupsAdopt: false);
        var lines = text.Split('\n');
        for (var i = BodyStart(lines); i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var number = i + 1;
            if (line.Length == 0 || line.StartsWith(StartTag, StringComparison.Ordinal))
            {
                continue;
            }
            if (line.StartsWith(CommentStart))
            {
                LayoutComment.Read(line[1..].Trim(), number, builder);
                continue;
            }
            if (line.StartsWith(EndTag, StringComparison.Ordinal))
            {
                return builder.Build();
            }
            ReadStatement(line, number, builder);
        }
        throw new DiagramValidationException("Missing @enduml.");
    }

    /// <summary>The index of the line after <c>@startuml</c>; only blank lines and comments may precede it.</summary>
    private static int BodyStart(string[] lines)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith(CommentStart))
            {
                continue;
            }
            return line.StartsWith(StartTag, StringComparison.Ordinal)
                ? i + 1
                : throw ParsedDiagramBuilder.Error(i + 1, "expected @startuml.");
        }
        throw new DiagramValidationException("PlantUML text must start with @startuml.");
    }

    private static void ReadStatement(string line, int number, ParsedDiagramBuilder builder)
    {
        var understood = TryReadSetting(line, builder)
            || TryReadTitle(line, builder)
            || TryReadGroupEnd(line, number, builder)
            || TryReadGroup(line, number, builder)
            || TryReadElement(line, number, builder)
            || AssociationStatement.TryRead(line, number, builder);
        if (!understood)
        {
            throw ParsedDiagramBuilder.Error(number, $"unsupported syntax '{line}'.");
        }
    }

    /// <summary>Orthogonal lines make all edges angled; other appearance settings have no counterpart in Kandinsky.</summary>
    private static bool TryReadSetting(string line, ParsedDiagramBuilder builder)
    {
        if (OrthoPattern().IsMatch(line))
        {
            builder.DefaultRouting = EdgeRouting.Angled;
            return true;
        }
        return IgnoredPattern().IsMatch(line);
    }

    private static bool TryReadTitle(string line, ParsedDiagramBuilder builder)
    {
        var match = TitlePattern().Match(line);
        if (match.Success)
        {
            builder.Title = match.Groups["title"].Value.Trim();
        }
        return match.Success;
    }

    private static bool TryReadGroupEnd(string line, int number, ParsedDiagramBuilder builder)
    {
        if (line != GroupEnd)
        {
            return false;
        }
        builder.EndGroup(number, GroupEnd);
        return true;
    }

    private static bool TryReadGroup(string line, int number, ParsedDiagramBuilder builder)
    {
        var match = GroupPattern().Match(line);
        var closedInline = match.Groups["close"].Success;
        // An empty "rectangle X {}" stays a plain rectangle.
        if (!match.Success || (closedInline && match.Groups["keyword"].Value == "rectangle"))
        {
            return false;
        }
        builder.BeginGroup(number, OptionalValue(match, "id"), OptionalValue(match, "label"));
        if (closedInline)
        {
            builder.EndGroup(number, GroupEnd);
        }
        return true;
    }

    private static bool TryReadElement(string line, int number, ParsedDiagramBuilder builder)
    {
        var match = ElementPattern().Match(line);
        if (!match.Success)
        {
            return false;
        }
        if (match.Groups["body"].Success && !match.Groups["close"].Success)
        {
            throw ParsedDiagramBuilder.Error(number, "class members are not supported yet.");
        }
        builder.DeclareShape(match.Groups["id"].Value, OptionalValue(match, "label"), PlantUmlElements.ImportedKind(match.Groups["keyword"].Value));
        return true;
    }

    private static string? OptionalValue(Match match, string group) => match.Groups[group].Success ? match.Groups[group].Value : null;

    [GeneratedRegex(@"^(skinparam\b|hide\b|show\b|allowmixing$|left to right direction$|top to bottom direction$)")]
    private static partial Regex IgnoredPattern();

    [GeneratedRegex(@"^skinparam\s+linetype\s+ortho$", RegexOptions.IgnoreCase)]
    private static partial Regex OrthoPattern();

    [GeneratedRegex(@"^title\s+(?<title>.+)$")]
    private static partial Regex TitlePattern();

    // A container with an opening brace: package "Label" as id {, package id {, package "Label" {, package com.example {.
    [GeneratedRegex(@"^(?<keyword>package|namespace|rectangle|frame|folder|node|cloud|component)\s+(?:""(?<label>[^""]+)""\s+as\s+(?<id>\w+)|(?<id>\w+)\s+as\s+""(?<label>[^""]+)""|(?<id>\w+)|""(?<label>[^""]+)""|(?<label>[\w.]+))\s*(?:<<[^>]*>>\s*)?\{\s*(?<close>\})?$")]
    private static partial Regex GroupPattern();

    [GeneratedRegex(@"^(?<keyword>abstract\s+class|abstract|class|interface|enum|rectangle|database|hexagon|diamond|<>|circle|usecase|storage|file)\s+(?:""(?<label>[^""]+)""\s+as\s+(?<id>\w+)|(?<id>\w+)\s+as\s+""(?<label>[^""]+)""|(?<id>\w+))\s*(?<body>\{\s*(?<close>\})?)?$")]
    private static partial Regex ElementPattern();
}
