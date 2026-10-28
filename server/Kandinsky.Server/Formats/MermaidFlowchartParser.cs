using System.Text.RegularExpressions;
using Kandinsky.Server.Model;

namespace Kandinsky.Server.Formats;

/// <summary>Reads the body of a Mermaid flowchart: nodes, subgraphs (as groups) and chains of directed links.</summary>
internal static partial class MermaidFlowchartParser
{
    private const string SubgraphEnd = "end";

    /// <summary>Reads the lines from <paramref name="start"/>, the line after the <c>flowchart</c> header.</summary>
    public static ParsedDiagram Parse(string[] lines, int start, ParsedDiagramBuilder builder)
    {
        for (var i = start; i < lines.Length; i++)
        {
            var line = lines[i].Trim().TrimEnd(';').TrimEnd();
            var number = i + 1;
            if (line.Length == 0 || MermaidComments.TryRead(line, number, builder))
            {
                continue;
            }
            ReadLine(line, number, builder);
        }
        return builder.Build();
    }

    private static void ReadLine(string line, int number, ParsedDiagramBuilder builder)
    {
        // Styling has no counterpart in Kandinsky.
        if (StylePattern().IsMatch(line))
        {
            return;
        }
        if (line == SubgraphEnd)
        {
            builder.EndGroup(number, SubgraphEnd);
            return;
        }
        var subgraph = SubgraphPattern().Match(line);
        if (subgraph.Success)
        {
            var label = subgraph.Groups["label"].Success ? LabelText(subgraph.Groups["label"].Value) : null;
            builder.BeginGroup(number, subgraph.Groups["id"].Success ? subgraph.Groups["id"].Value : null, label);
            return;
        }
        ReadLinkChain(line, number, builder);
    }

    /// <summary>Reads <c>A[label] --&gt; B &amp; C --&gt;|label| D</c>: a chain of node groups joined by directed links.</summary>
    private static void ReadLinkChain(string line, int number, ParsedDiagramBuilder builder)
    {
        var position = 0;
        var sources = ReadNodeGroup(line, ref position, number, builder);
        while (position < line.Length)
        {
            var link = LinkPattern().Match(line, position);
            if (!link.Success)
            {
                throw ParsedDiagramBuilder.Error(number, OtherLinkPattern().IsMatch(line, position)
                    ? "only directed links ('-->', '-.->', '==>') are supported."
                    : $"unsupported syntax '{line}'.");
            }
            position += link.Length;
            var targets = ReadNodeGroup(line, ref position, number, builder);
            var label = link.Groups["label"].Success ? LabelText(link.Groups["label"].Value) : null;
            AddLinks(sources, targets, label, number, builder);
            sources = targets;
        }
    }

    private static void AddLinks(List<string> sources, List<string> targets, string? label, int number, ParsedDiagramBuilder builder)
    {
        foreach (var (source, target) in sources.SelectMany(source => targets.Select(target => (source, target))))
        {
            builder.AddEdge(number, source, target, label);
        }
    }

    /// <summary>Reads <c>A &amp; B &amp; …</c> and returns the node ids.</summary>
    private static List<string> ReadNodeGroup(string line, ref int position, int number, ParsedDiagramBuilder builder)
    {
        var ids = new List<string> { ReadNode(line, ref position, number, builder) };
        for (var and = AndPattern().Match(line, position); and.Success; and = AndPattern().Match(line, position))
        {
            position += and.Length;
            ids.Add(ReadNode(line, ref position, number, builder));
        }
        return ids;
    }

    private static string ReadNode(string line, ref int position, int number, ParsedDiagramBuilder builder)
    {
        var node = NodePattern().Match(line, position);
        if (!node.Success)
        {
            throw ParsedDiagramBuilder.Error(number, $"unsupported syntax '{line}'.");
        }
        position += node.Length;
        var id = node.Groups["id"].Value;
        var (label, kind) = node.Groups["props"].Success ? V11Node(node.Groups["props"].Value) : ClassicNode(node);
        builder.DeclareShape(id, label, kind);
        return id;
    }

    /// <summary>The v11 shape syntax: <c>A@{ shape: doc, label: "text" }</c>.</summary>
    private static (string? Label, ShapeKind? Kind) V11Node(string properties)
    {
        var shape = ShapePropertyPattern().Match(properties);
        var label = LabelPropertyPattern().Match(properties);
        return (
            label.Success ? LabelText(label.Groups["label"].Value) : null,
            shape.Success ? MermaidShapeSyntax.V11Kind(shape.Groups["shape"].Value) : null);
    }

    private static (string? Label, ShapeKind? Kind) ClassicNode(Match node)
    {
        var open = node.Groups["open"];
        return (
            node.Groups["text"].Success ? LabelText(node.Groups["text"].Value) : null,
            open.Success ? MermaidShapeSyntax.ClassicKind(open.Value, node.Groups["close"].Value) : null);
    }

    // Labels are single-line: line breaks become spaces.
    private static string LabelText(string text) =>
        WhitespacePattern().Replace(LineBreakPattern().Replace(text.Trim().Trim('"'), " "), " ").Trim();

    // subgraph id, subgraph id [title], subgraph id["title"], or subgraph followed by a title only (the id is generated).
    [GeneratedRegex(@"^subgraph\s+(?:(?<id>\w+)\s*(?:\[\s*(?:""(?<label>[^""]*)""|(?<label>[^\]""]*?))\s*\])?|""(?<label>[^""]+)""|(?<label>.+?))$")]
    private static partial Regex SubgraphPattern();

    [GeneratedRegex(@"^(?:style|classDef|class|linkStyle|click|direction)\s")]
    private static partial Regex StylePattern();

    // A node id with an optional shape such as [text], (text), [(text)] (database), {text}, ((text)) or "quoted" text,
    // or the v11 shape syntax @{ shape: …, label: … }, and an optional :::class.
    [GeneratedRegex(@"\G\s*(?<id>\w+)(?:@\{(?<props>[^}]*)\}|(?<open>\(\(\(|\(\[|\[\[|\[\(|\(\(|\{\{|\[/|\[\\|\[|\(|\{|>)\s*(?:""(?<text>[^""]*)""|(?<text>[^""]*?))\s*(?<close>\)\)\)|\]\)|\]\]|\)\]|\)\)|\}\}|/\]|\\\]|\]|\)|\}))?(?::::\w+)?\s*")]
    private static partial Regex NodePattern();

    // -->, -.->, ==> (any length) with an optional |label|, or the text forms -- label -->, -. label .->, == label ==>.
    [GeneratedRegex(@"\G\s*(?:(?:-{2,}>|-\.+->|={2,}>)(?:\s*\|(?:""(?<label>[^""]*)""|(?<label>[^|]*))\|)?|--\s*(?<label>[^>]+?)\s*-{2,}>|-\.\s*(?<label>[^>]+?)\s*\.+->|==\s*(?<label>[^>]+?)\s*={2,}>)\s*")]
    private static partial Regex LinkPattern();

    // Undirected and other links: ---, -.-, ===, ~~~, --o, --x, <-->.
    [GeneratedRegex(@"\G\s*[<ox]?[-=.~]{2,}")]
    private static partial Regex OtherLinkPattern();

    [GeneratedRegex(@"\bshape:\s*(?<shape>[\w-]+)")]
    private static partial Regex ShapePropertyPattern();

    [GeneratedRegex(@"\blabel:\s*(?:""(?<label>[^""]*)""|(?<label>[^,]*))")]
    private static partial Regex LabelPropertyPattern();

    [GeneratedRegex(@"\G\s*&\s*")]
    private static partial Regex AndPattern();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreakPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
