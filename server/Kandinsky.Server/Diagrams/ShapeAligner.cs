using Kandinsky.Server.Model;

namespace Kandinsky.Server.Diagrams;

/// <summary>Aligns shapes in a row or column, centered on the first (anchor) shape.</summary>
internal static class ShapeAligner
{
    public static IReadOnlyList<Shape> Align(IReadOnlyList<Shape> shapes, IReadOnlyList<string> shapeIds, AlignDirection direction)
    {
        var anchor = shapes.First(s => s.Id == shapeIds[0]);
        var selectedIds = shapeIds.ToHashSet();
        var aligned = shapes
            .Where(s => selectedIds.Contains(s.Id))
            .ToDictionary(s => s.Id, s => AlignedTo(anchor, s, direction));
        var originals = shapes.ToDictionary(s => s.Id);

        return shapes
            .Select(s => aligned.TryGetValue(s.Id, out var alignedShape) ? alignedShape : FollowAlignedGroup(s, aligned, originals))
            .ToList();
    }

    private static Shape AlignedTo(Shape anchor, Shape shape, AlignDirection direction) => direction == AlignDirection.Horizontal
        ? shape with { Y = Math.Max(0, anchor.Y + anchor.Height / 2 - shape.Height / 2) }
        : shape with { X = Math.Max(0, anchor.X + anchor.Width / 2 - shape.Width / 2) };

    /// <summary>Shapes inside an aligned group move along with the innermost such group, unless they are aligned themselves.</summary>
    private static Shape FollowAlignedGroup(Shape shape, Dictionary<string, Shape> aligned, Dictionary<string, Shape> originals)
    {
        for (var groupId = shape.ParentId; groupId is not null; groupId = originals[groupId].ParentId)
        {
            if (aligned.TryGetValue(groupId, out var movedGroup))
            {
                var group = originals[groupId];
                return shape.MovedBy(movedGroup.X - group.X, movedGroup.Y - group.Y);
            }
        }
        return shape;
    }
}
