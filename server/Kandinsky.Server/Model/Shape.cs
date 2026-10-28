namespace Kandinsky.Server.Model;

/// <summary>
/// A shape on the diagram canvas, drawn within its bounds. ParentId is the group the shape lies in
/// (the smallest group whose bounds fully contain it); the server derives it from the positions, so it is read-only.
/// </summary>
public sealed record Shape(string Id, string Label, double X, double Y, double Width, double Height, ShapeKind Kind = ShapeKind.Rectangle, string? ParentId = null)
{
    public double Area() => Width * Height;

    public bool Contains(Shape inner) =>
        inner.X >= X && inner.Y >= Y && inner.X + inner.Width <= X + Width && inner.Y + inner.Height <= Y + Height;

    public bool Overlaps(double x, double y, double width, double height) =>
        x < X + Width && X < x + width && y < Y + Height && Y < y + height;

    public Shape MovedBy(double deltaX, double deltaY) => this with { X = X + deltaX, Y = Y + deltaY };
}
