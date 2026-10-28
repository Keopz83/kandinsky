using Kandinsky.Server.Model;

namespace Kandinsky.Server.Diagrams;

/// <summary>Places a shape added without a position in the first slot of a grid where it overlaps no other shape.</summary>
internal static class FreeGridSlot
{
    private const int Columns = 5;
    private const double ColumnSpacing = 200;
    private const double RowSpacing = 120;
    private const double Origin = 40;

    public static (double X, double Y) Find(IReadOnlyCollection<Shape> shapes, double width, double height)
    {
        for (var slot = 0; ; slot++)
        {
            var x = Origin + slot % Columns * ColumnSpacing;
            var y = Origin + slot / Columns * RowSpacing;
            if (!shapes.Any(s => s.Overlaps(x, y, width, height)))
            {
                return (x, y);
            }
        }
    }
}
