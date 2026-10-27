using System.Net;
using System.Net.Http.Json;
using Kandinsky.Server.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Kandinsky.Server.Tests;

public sealed class ApiTests : IDisposable
{
    private readonly string _storage = Path.Combine(Path.GetTempPath(), "kandinsky-tests-" + Guid.NewGuid().ToString("N"));
    private readonly List<WebApplicationFactory<Program>> _factories = [];

    public void Dispose()
    {
        foreach (var factory in _factories)
        {
            factory.Dispose();
        }
        if (Directory.Exists(_storage))
        {
            Directory.Delete(_storage, recursive: true);
        }
    }

    private HttpClient CreateClient()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("Storage:Path", _storage));
        _factories.Add(factory);
        return factory.CreateClient();
    }

    private static async Task<T> Read<T>(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    [Fact]
    public async Task Creates_diagram_with_shapes_and_edge()
    {
        var client = CreateClient();

        var diagram = await Read<Diagram>(await client.PostAsJsonAsync("/api/diagrams", new { name = "Shop" }), HttpStatusCode.Created);
        var customer = await Read<Shape>(await client.PostAsJsonAsync($"/api/diagrams/{diagram.Id}/shapes", new { label = "Customer" }), HttpStatusCode.Created);
        var order = await Read<Shape>(await client.PostAsJsonAsync($"/api/diagrams/{diagram.Id}/shapes", new { label = "Order" }), HttpStatusCode.Created);
        var edge = await Read<Edge>(
            await client.PostAsJsonAsync($"/api/diagrams/{diagram.Id}/edges", new { sourceId = customer.Id, targetId = order.Id, label = "places" }),
            HttpStatusCode.Created);

        var loaded = await client.GetFromJsonAsync<Diagram>($"/api/diagrams/{diagram.Id}");
        Assert.Equal("Shop", loaded!.Name);
        Assert.Equal(["Customer", "Order"], loaded.Shapes.Select(s => s.Label));
        Assert.NotEqual((customer.X, customer.Y), (order.X, order.Y));
        Assert.Equal(new Edge(edge.Id, customer.Id, order.Id, "places"), Assert.Single(loaded.Edges));
    }

    [Fact]
    public async Task Updates_shape_partially()
    {
        var client = CreateClient();
        var diagram = await Read<Diagram>(await client.PostAsJsonAsync("/api/diagrams", new { }), HttpStatusCode.Created);
        var shape = await Read<Shape>(await client.PostAsJsonAsync($"/api/diagrams/{diagram.Id}/shapes", new { label = "A", x = 10, y = 20 }), HttpStatusCode.Created);

        var updated = await Read<Shape>(await client.PatchAsJsonAsync($"/api/diagrams/{diagram.Id}/shapes/{shape.Id}", new { x = 300 }), HttpStatusCode.OK);

        Assert.Equal(shape with { X = 300 }, updated);
    }

    [Fact]
    public async Task Deleting_shape_removes_connected_edges()
    {
        var client = CreateClient();
        var diagram = await Read<Diagram>(await client.PostAsJsonAsync("/api/diagrams", new { }), HttpStatusCode.Created);
        var a = await Read<Shape>(await client.PostAsJsonAsync($"/api/diagrams/{diagram.Id}/shapes", new { label = "A" }), HttpStatusCode.Created);
        var b = await Read<Shape>(await client.PostAsJsonAsync($"/api/diagrams/{diagram.Id}/shapes", new { label = "B" }), HttpStatusCode.Created);
        await client.PostAsJsonAsync($"/api/diagrams/{diagram.Id}/edges", new { sourceId = a.Id, targetId = b.Id });

        var response = await client.DeleteAsync($"/api/diagrams/{diagram.Id}/shapes/{a.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var loaded = await client.GetFromJsonAsync<Diagram>($"/api/diagrams/{diagram.Id}");
        Assert.Equal([b], loaded!.Shapes);
        Assert.Empty(loaded.Edges);
    }

    [Fact]
    public async Task Rejects_edge_to_unknown_shape()
    {
        var client = CreateClient();
        var diagram = await Read<Diagram>(await client.PostAsJsonAsync("/api/diagrams", new { }), HttpStatusCode.Created);
        var a = await Read<Shape>(await client.PostAsJsonAsync($"/api/diagrams/{diagram.Id}/shapes", new { label = "A" }), HttpStatusCode.Created);

        var problem = await Read<ProblemDetails>(
            await client.PostAsJsonAsync($"/api/diagrams/{diagram.Id}/edges", new { sourceId = a.Id, targetId = "nope" }),
            HttpStatusCode.BadRequest);

        Assert.Equal("Shape 'nope' does not exist in this diagram.", problem.Detail);
    }

    [Fact]
    public async Task Returns_not_found_for_unknown_diagram()
    {
        var client = CreateClient();

        var problem = await Read<ProblemDetails>(await client.GetAsync("/api/diagrams/unknown"), HttpStatusCode.NotFound);

        Assert.Equal("Diagram 'unknown' not found.", problem.Detail);
    }

    [Fact]
    public async Task Saved_diagram_survives_restart()
    {
        var client = CreateClient();
        var diagram = await Read<Diagram>(await client.PostAsJsonAsync("/api/diagrams", new { name = "Kept" }), HttpStatusCode.Created);
        await client.PostAsJsonAsync($"/api/diagrams/{diagram.Id}/shapes", new { label = "A" });
        await client.PostAsJsonAsync("/api/diagrams", new { name = "Not saved" });

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/diagrams/{diagram.Id}/save", null)).StatusCode);

        var restarted = CreateClient();
        var list = await restarted.GetFromJsonAsync<DiagramSummary[]>("/api/diagrams");
        Assert.Equal([new DiagramSummary(diagram.Id, "Kept")], list!);
        var loaded = await restarted.GetFromJsonAsync<Diagram>($"/api/diagrams/{diagram.Id}");
        Assert.Equal("A", Assert.Single(loaded!.Shapes).Label);
    }

    [Fact]
    public async Task Imports_and_exports_text()
    {
        var client = CreateClient();
        var imported = await Read<Diagram>(
            await client.PostAsJsonAsync("/api/diagrams/import", new { text = "@startuml\ntitle Zoo\nCat --> Animal : is a\n@enduml" }),
            HttpStatusCode.Created);

        var mermaid = await client.GetStringAsync($"/api/diagrams/{imported.Id}/export?format=mermaid");

        Assert.Equal("Zoo", imported.Name);
        Assert.Equal(
            "---\ntitle: \"Zoo\"\n---\nclassDiagram\n  class Cat[\"Cat\"]\n  class Animal[\"Animal\"]\n  Cat --> Animal : is a\n",
            mermaid);
    }

    [Fact]
    public async Task Reports_import_errors()
    {
        var client = CreateClient();

        var problem = await Read<ProblemDetails>(
            await client.PostAsJsonAsync("/api/diagrams/import", new { text = "@startuml\nA -- B\n@enduml" }),
            HttpStatusCode.BadRequest);

        Assert.Equal("Line 2: unsupported syntax 'A -- B'.", problem.Detail);
    }
}
