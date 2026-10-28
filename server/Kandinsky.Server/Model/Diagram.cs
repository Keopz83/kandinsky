namespace Kandinsky.Server.Model;

/// <summary>A diagram with its shapes and edges.</summary>
public sealed record Diagram(string Id, string Name, IReadOnlyList<Shape> Shapes, IReadOnlyList<Edge> Edges)
{
    /// <summary>Whether there are edges and all of them are angled, which the text formats can express as a global setting.</summary>
    public bool HasOnlyAngledEdges() => Edges.Count > 0 && Edges.All(e => e.Routing == EdgeRouting.Angled);
}
