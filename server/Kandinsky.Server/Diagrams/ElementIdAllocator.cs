using Kandinsky.Server.Model;

namespace Kandinsky.Server.Diagrams;

/// <summary>Hands out shape and edge ids (<c>s1</c>, <c>e1</c>, …) that are not used yet; shapes and edges share one id space.</summary>
internal sealed class ElementIdAllocator(IEnumerable<string> usedIds)
{
    private const string ShapePrefix = "s";
    private const string EdgePrefix = "e";

    private readonly HashSet<string> _usedIds = usedIds.ToHashSet();

    public static ElementIdAllocator For(Diagram diagram) =>
        new(diagram.Shapes.Select(s => s.Id).Concat(diagram.Edges.Select(e => e.Id)));

    public string NextShapeId() => Next(ShapePrefix);

    public string NextEdgeId() => Next(EdgePrefix);

    /// <summary>Takes the preferred id if it is still free, else a new shape id.</summary>
    public string ClaimShapeId(string preferredId) => _usedIds.Add(preferredId) ? preferredId : NextShapeId();

    private string Next(string prefix)
    {
        for (var number = 1; ; number++)
        {
            var id = prefix + number;
            if (_usedIds.Add(id))
            {
                return id;
            }
        }
    }
}
