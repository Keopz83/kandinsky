using Kandinsky.Server.Diagrams;
using Kandinsky.Server.Model;

namespace Kandinsky.Server.Formats;

/// <summary>The shapes of a diagram nested by group membership, for formats that write group content inside the group.</summary>
internal sealed class ShapeTree
{
    private const string TopLevelKey = "";

    private readonly ILookup<string, Shape> _children;

    public ShapeTree(IReadOnlyList<Shape> shapes)
    {
        var parentIds = DiagramGroups.Parents(shapes);
        _children = shapes.ToLookup(s => parentIds[s.Id] ?? TopLevelKey);
    }

    public IEnumerable<Shape> TopLevel => _children[TopLevelKey];

    public IEnumerable<Shape> ChildrenOf(Shape group) => _children[group.Id];
}
