using Kandinsky.Server.Diagrams;
using Kandinsky.Server.Model;

namespace Kandinsky.Server.Tests;

public class LayoutTests
{
    private static Shape S(string id) => new(id, id, 500, 500, 140, 60);

    private static Edge E(string from, string to) => new($"{from}-{to}", from, to, null);

    private static Dictionary<string, Shape> Arrange(Shape[] shapes, Edge[] edges) =>
        DiagramLayout.Arrange(shapes, edges).ToDictionary(s => s.Id);

    [Fact]
    public void Places_chain_top_to_bottom_and_unconnected_shapes_below()
    {
        var result = Arrange([S("A"), S("B"), S("C"), S("Lonely")], [E("A", "B"), E("B", "C")]);

        Assert.True(result["A"].Y < result["B"].Y);
        Assert.True(result["B"].Y < result["C"].Y);
        Assert.Equal(result["A"].X, result["C"].X);
        Assert.True(result["Lonely"].Y > result["C"].Y);
    }

    [Fact]
    public void Puts_siblings_in_one_row_and_centers_the_join_below()
    {
        var result = Arrange([S("A"), S("B"), S("C"), S("D")], [E("A", "B"), E("A", "C"), E("B", "D"), E("C", "D")]);

        Assert.Equal(result["B"].Y, result["C"].Y);
        Assert.NotEqual(result["B"].X, result["C"].X);
        Assert.Equal(result["A"].X, result["D"].X);
    }

    [Fact]
    public void Orders_layers_to_avoid_crossings()
    {
        var result = Arrange([S("A"), S("B"), S("C"), S("D")], [E("A", "D"), E("B", "C")]);

        Assert.True(result["A"].X < result["B"].X);
        Assert.True(result["D"].X < result["C"].X);
    }

    [Fact]
    public void Handles_cycles()
    {
        var result = Arrange([S("A"), S("B"), S("C")], [E("A", "B"), E("B", "C"), E("C", "A")]);

        Assert.True(result["A"].Y < result["B"].Y);
        Assert.True(result["B"].Y < result["C"].Y);
    }

    [Fact]
    public void Keeps_shape_sizes_and_labels()
    {
        var big = new Shape("Big", "Big one", 0, 0, 300, 120);

        var result = Arrange([big, S("A")], [E("Big", "A")]);

        Assert.Equal(big with { X = result["Big"].X, Y = result["Big"].Y }, result["Big"]);
    }

    [Fact]
    public void Lays_out_group_content_inside_the_group_and_fits_the_group_to_it()
    {
        Shape In(string id, string group) => S(id) with { ParentId = group };
        var outer = new Shape("G", "G", 0, 0, 100, 100, ShapeKind.Group);
        var inner = new Shape("H", "H", 0, 0, 100, 100, ShapeKind.Group, "G");
        var empty = new Shape("Empty", "Empty", 0, 0, 320, 200, ShapeKind.Group);

        var result = Arrange(
            [S("Top"), outer, In("A", "G"), In("B", "G"), inner, In("C", "H"), empty],
            [E("Top", "A"), E("A", "B"), E("B", "C")]);

        Assert.True(result["Top"].Y + result["Top"].Height < result["G"].Y);
        Assert.True(result["A"].Y < result["B"].Y);
        Assert.True(result["B"].Y + result["B"].Height < result["H"].Y);
        foreach (var (id, group) in new[] { ("A", "G"), ("B", "G"), ("H", "G"), ("C", "H") })
        {
            var (s, g) = (result[id], result[group]);
            Assert.True(s.X > g.X && s.Y > g.Y && s.X + s.Width < g.X + g.Width && s.Y + s.Height < g.Y + g.Height, $"{id} inside {group}");
        }
        Assert.Equal((320, 200), (result["Empty"].Width, result["Empty"].Height));
        Assert.Equal(
            result.Values.ToDictionary(s => s.Id, s => s.ParentId),
            DiagramGroups.Parents([.. result.Values]));
    }
}
