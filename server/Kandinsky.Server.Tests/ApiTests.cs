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
    public async Task Adds_and_updates_database_shapes()
    {
        var client = CreateClient();
        var diagram = await Read<Diagram>(await client.PostAsJsonAsync("/api/diagrams", new { }), HttpStatusCode.Created);

        var response = await client.PostAsJsonAsync($"/api/diagrams/{diagram.Id}/shapes", new { kind = "database", x = 10, y = 20 });
        Assert.Contains("\"kind\":\"database\"", await response.Content.ReadAsStringAsync());
        var database = await Read<Shape>(response, HttpStatusCode.Created);
        var rectangle = await Read<Shape>(await client.PostAsJsonAsync($"/api/diagrams/{diagram.Id}/shapes", new { label = "A" }), HttpStatusCode.Created);
        var changed = await Read<Shape>(await client.PatchAsJsonAsync($"/api/diagrams/{diagram.Id}/shapes/{rectangle.Id}", new { kind = "database" }), HttpStatusCode.OK);

        Assert.Equal(("Database", ShapeKind.Database), (database.Label, database.Kind));
        Assert.Equal(ShapeKind.Rectangle, rectangle.Kind);
        Assert.Equal(rectangle with { Kind = ShapeKind.Database }, changed);
    }

    [Fact]
    public async Task Imports_and_pastes_mermaid_flowchart_databases()
    {
        var client = CreateClient();
        var text = """
            ```mermaid
            flowchart LR
                ASSET[Asset.API] --> DB[(Oracle<br/>source of truth)]
                DB --> VIEW[DB View]
            ```
            """;

        var imported = await Read<Diagram>(await client.PostAsJsonAsync("/api/diagrams/import", new { text }), HttpStatusCode.Created);
        var pasted = await Read<PasteResult>(await client.PostAsJsonAsync($"/api/diagrams/{imported.Id}/paste", new { text }), HttpStatusCode.OK);

        Assert.Equal(
            [ShapeKind.Rectangle, ShapeKind.Database, ShapeKind.Rectangle],
            imported.Shapes.Select(s => s.Kind));
        Assert.Equal(
            [ShapeKind.Rectangle, ShapeKind.Database, ShapeKind.Rectangle],
            pasted.Diagram.Shapes.Where(s => pasted.ShapeIds.Contains(s.Id)).Select(s => s.Kind));
        Assert.Contains("[(\"Oracle source of truth\")]", await client.GetStringAsync($"/api/diagrams/{imported.Id}/export?format=mermaid"));
        Assert.Contains("database \"Oracle source of truth\" as DB", await client.GetStringAsync($"/api/diagrams/{imported.Id}/export?format=plantuml"));
    }

    [Fact]
    public async Task Adds_decisions_and_groups_with_their_default_label_and_size()
    {
        var client = CreateClient();
        var diagram = await Read<Diagram>(await client.PostAsJsonAsync("/api/diagrams", new { }), HttpStatusCode.Created);

        var decision = await Read<Shape>(await client.PostAsJsonAsync($"/api/diagrams/{diagram.Id}/shapes", new { kind = "decision", x = 0, y = 0 }), HttpStatusCode.Created);
        var group = await Read<Shape>(await client.PostAsJsonAsync($"/api/diagrams/{diagram.Id}/shapes", new { kind = "group", x = 200, y = 0 }), HttpStatusCode.Created);

        Assert.Equal(new Shape(decision.Id, "Decision", 0, 0, 140, 80, ShapeKind.Decision), decision);
        Assert.Equal(new Shape(group.Id, "Group", 200, 0, 320, 200, ShapeKind.Group), group);
    }

    [Fact]
    public async Task Group_membership_follows_the_position()
    {
        var client = CreateClient();
        var diagram = await Read<Diagram>(await client.PostAsJsonAsync("/api/diagrams", new { }), HttpStatusCode.Created);
        var url = $"/api/diagrams/{diagram.Id}/shapes";
        var outer = await Read<Shape>(await client.PostAsJsonAsync(url, new { kind = "group", x = 0, y = 0, width = 600, height = 400 }), HttpStatusCode.Created);
        var inner = await Read<Shape>(await client.PostAsJsonAsync(url, new { kind = "group", x = 20, y = 40, width = 300, height = 200 }), HttpStatusCode.Created);
        var shape = await Read<Shape>(await client.PostAsJsonAsync(url, new { x = 40, y = 80 }), HttpStatusCode.Created);

        Assert.Equal((null, outer.Id, inner.Id), (outer.ParentId, inner.ParentId, shape.ParentId));

        // Partly outside the inner group: only in the outer one; moved out completely: at the top level.
        var moved = await Read<Shape>(await client.PatchAsJsonAsync($"{url}/{shape.Id}", new { x = 250 }), HttpStatusCode.OK);
        Assert.Equal(outer.Id, moved.ParentId);
        moved = await Read<Shape>(await client.PatchAsJsonAsync($"{url}/{shape.Id}", new { x = 700 }), HttpStatusCode.OK);
        Assert.Null(moved.ParentId);

        // Growing a group around a shape makes the shape its member.
        await client.PatchAsJsonAsync($"{url}/{outer.Id}", new { width = 900 });
        Assert.Equal(outer.Id, (await client.GetFromJsonAsync<Shape>($"{url}/{shape.Id}"))!.ParentId);
    }

    [Fact]
    public async Task Deleting_a_group_deletes_its_content_and_their_edges()
    {
        var client = CreateClient();
        var diagram = await Read<Diagram>(await client.PostAsJsonAsync("/api/diagrams", new { }), HttpStatusCode.Created);
        var url = $"/api/diagrams/{diagram.Id}";
        var group = await Read<Shape>(await client.PostAsJsonAsync($"{url}/shapes", new { kind = "group", x = 0, y = 0, width = 600, height = 400 }), HttpStatusCode.Created);
        await client.PostAsJsonAsync($"{url}/shapes", new { kind = "group", x = 20, y = 40, width = 300, height = 200 });
        var inside = await Read<Shape>(await client.PostAsJsonAsync($"{url}/shapes", new { x = 40, y = 80 }), HttpStatusCode.Created);
        var outside = await Read<Shape>(await client.PostAsJsonAsync($"{url}/shapes", new { x = 700, y = 0 }), HttpStatusCode.Created);
        await client.PostAsJsonAsync($"{url}/edges", new { sourceId = outside.Id, targetId = inside.Id });

        var response = await client.DeleteAsync($"{url}/shapes/{group.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var loaded = await client.GetFromJsonAsync<Diagram>(url);
        Assert.Equal([outside], loaded!.Shapes);
        Assert.Empty(loaded.Edges);
    }

    [Fact]
    public async Task Aligning_a_group_moves_its_content_along()
    {
        var client = CreateClient();
        var diagram = await Read<Diagram>(await client.PostAsJsonAsync("/api/diagrams", new { }), HttpStatusCode.Created);
        var url = $"/api/diagrams/{diagram.Id}";
        var anchor = await Read<Shape>(await client.PostAsJsonAsync($"{url}/shapes", new { x = 0, y = 500 }), HttpStatusCode.Created);
        var group = await Read<Shape>(await client.PostAsJsonAsync($"{url}/shapes", new { kind = "group", x = 300, y = 0, width = 300, height = 200 }), HttpStatusCode.Created);
        var inside = await Read<Shape>(await client.PostAsJsonAsync($"{url}/shapes", new { x = 320, y = 40 }), HttpStatusCode.Created);

        var aligned = await Read<Diagram>(
            await client.PostAsJsonAsync($"{url}/align", new { shapeIds = new[] { anchor.Id, group.Id }, direction = "horizontal" }),
            HttpStatusCode.OK);

        var shapes = aligned.Shapes.ToDictionary(s => s.Id);
        Assert.Equal(430, shapes[group.Id].Y);
        Assert.Equal(inside with { Y = 470 }, shapes[inside.Id]);
    }

    [Fact]
    public async Task Imports_mermaid_subgraphs_without_layout_with_the_content_inside_the_groups()
    {
        var client = CreateClient();
        var text = """
            flowchart TB
              Start --> Check{Valid?}
              subgraph backend [Back end]
                API --> DB[(Oracle)]
              end
              Check -->|yes| API
              Check -->|no| Start
            """;

        var imported = await Read<Diagram>(await client.PostAsJsonAsync("/api/diagrams/import", new { text }), HttpStatusCode.Created);

        var shapes = imported.Shapes.ToDictionary(s => s.Id);
        Assert.Equal((ShapeKind.Decision, ShapeKind.Group), (shapes["Check"].Kind, shapes["backend"].Kind));
        Assert.Equal([null, null, null, "backend", "backend"], imported.Shapes.Select(s => s.ParentId));
        Assert.Equal(["yes", "no"], imported.Edges.Where(e => e.SourceId == "Check").Select(e => e.Label));
        var mermaid = await client.GetStringAsync($"/api/diagrams/{imported.Id}/export?format=mermaid");
        Assert.Contains("subgraph backend [\"Back end\"]\n    API[\"API\"]\n    DB[(\"Oracle\")]\n  end\n", mermaid);
        Assert.Contains("Check{\"Valid?\"}", mermaid);
    }

    [Fact]
    public async Task Adds_flowchart_symbols_with_their_default_label_and_size()
    {
        var client = CreateClient();
        var diagram = await Read<Diagram>(await client.PostAsJsonAsync("/api/diagrams", new { }), HttpStatusCode.Created);
        var url = $"/api/diagrams/{diagram.Id}/shapes";

        var start = await Read<Shape>(await client.PostAsJsonAsync(url, new { kind = "start", x = 0, y = 0 }), HttpStatusCode.Created);
        var io = await Read<Shape>(await client.PostAsJsonAsync(url, new { kind = "io", x = 0, y = 100 }), HttpStatusCode.Created);
        var document = await Read<Shape>(await client.PostAsJsonAsync(url, new { kind = "document", x = 0, y = 200 }), HttpStatusCode.Created);

        Assert.Equal(new Shape(start.Id, "Start", 0, 0, 40, 40, ShapeKind.Start), start);
        Assert.Equal(new Shape(io.Id, "Input / output", 0, 100, 140, 60, ShapeKind.InputOutput), io);
        Assert.Equal(new Shape(document.Id, "Document", 0, 200, 140, 70, ShapeKind.Document), document);
    }

    [Fact]
    public async Task Adds_angled_edges_and_switches_the_routing()
    {
        var client = CreateClient();
        var diagram = await Read<Diagram>(await client.PostAsJsonAsync("/api/diagrams", new { }), HttpStatusCode.Created);
        var url = $"/api/diagrams/{diagram.Id}";
        var a = await Read<Shape>(await client.PostAsJsonAsync($"{url}/shapes", new { label = "A" }), HttpStatusCode.Created);
        var b = await Read<Shape>(await client.PostAsJsonAsync($"{url}/shapes", new { label = "B" }), HttpStatusCode.Created);

        var response = await client.PostAsJsonAsync($"{url}/edges", new { sourceId = a.Id, targetId = b.Id, routing = "angled" });
        Assert.Contains("\"routing\":\"angled\"", await response.Content.ReadAsStringAsync());
        var edge = await Read<Edge>(response, HttpStatusCode.Created);
        var straight = await Read<Edge>(await client.PatchAsJsonAsync($"{url}/edges/{edge.Id}", new { routing = "straight" }), HttpStatusCode.OK);
        var relabeled = await Read<Edge>(await client.PatchAsJsonAsync($"{url}/edges/{edge.Id}", new { label = "next" }), HttpStatusCode.OK);

        Assert.Equal(EdgeRouting.Angled, edge.Routing);
        Assert.Equal(edge with { Routing = EdgeRouting.Straight }, straight);
        Assert.Equal(straight with { Label = "next" }, relabeled);
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
    public async Task Deletes_unsaved_diagram()
    {
        var client = CreateClient();
        var diagram = await Read<Diagram>(await client.PostAsJsonAsync("/api/diagrams", new { }), HttpStatusCode.Created);

        var response = await client.DeleteAsync($"/api/diagrams/{diagram.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/diagrams/{diagram.Id}")).StatusCode);
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
    public async Task Aligns_shapes_on_the_first_shape()
    {
        var client = CreateClient();
        var diagram = await Read<Diagram>(await client.PostAsJsonAsync("/api/diagrams", new { }), HttpStatusCode.Created);
        var anchor = await Read<Shape>(await client.PostAsJsonAsync($"/api/diagrams/{diagram.Id}/shapes", new { label = "A", x = 100, y = 100, width = 140, height = 60 }), HttpStatusCode.Created);
        var other = await Read<Shape>(await client.PostAsJsonAsync($"/api/diagrams/{diagram.Id}/shapes", new { label = "B", x = 400, y = 300, width = 100, height = 100 }), HttpStatusCode.Created);

        var aligned = await Read<Diagram>(
            await client.PostAsJsonAsync($"/api/diagrams/{diagram.Id}/align", new { shapeIds = new[] { anchor.Id, other.Id }, direction = "horizontal" }),
            HttpStatusCode.OK);

        Assert.Equal([anchor, other with { Y = 80 }], aligned.Shapes);

        aligned = await Read<Diagram>(
            await client.PostAsJsonAsync($"/api/diagrams/{diagram.Id}/align", new { shapeIds = new[] { anchor.Id, other.Id }, direction = "vertical" }),
            HttpStatusCode.OK);

        Assert.Equal([anchor, other with { X = 120, Y = 80 }], aligned.Shapes);
    }

    [Fact]
    public async Task Rejects_align_with_fewer_than_two_shapes()
    {
        var client = CreateClient();
        var diagram = await Read<Diagram>(await client.PostAsJsonAsync("/api/diagrams", new { }), HttpStatusCode.Created);
        var a = await Read<Shape>(await client.PostAsJsonAsync($"/api/diagrams/{diagram.Id}/shapes", new { label = "A" }), HttpStatusCode.Created);

        var problem = await Read<ProblemDetails>(
            await client.PostAsJsonAsync($"/api/diagrams/{diagram.Id}/align", new { shapeIds = new[] { a.Id }, direction = "vertical" }),
            HttpStatusCode.BadRequest);

        Assert.Equal("Select at least two shapes to align.", problem.Detail);
    }

    [Fact]
    public async Task Auto_layout_arranges_all_shapes()
    {
        var client = CreateClient();
        var imported = await Read<Diagram>(
            await client.PostAsJsonAsync("/api/diagrams/import", new { text = "classDiagram\n  C --> B\n  B --> A\n  class Lonely" }),
            HttpStatusCode.Created);

        var laidOut = await Read<Diagram>(await client.PostAsync($"/api/diagrams/{imported.Id}/layout", null), HttpStatusCode.OK);

        var y = laidOut.Shapes.ToDictionary(s => s.Id, s => s.Y);
        Assert.True(y["C"] < y["B"] && y["B"] < y["A"] && y["A"] < y["Lonely"]);
        Assert.Equal(laidOut, await client.GetFromJsonAsync<Diagram>($"/api/diagrams/{imported.Id}"), DiagramComparer);
    }

    private static readonly IEqualityComparer<Diagram?> DiagramComparer = EqualityComparer<Diagram?>.Create(
        (a, b) => a is not null && b is not null && a.Id == b.Id && a.Shapes.SequenceEqual(b.Shapes) && a.Edges.SequenceEqual(b.Edges));

    [Fact]
    public async Task Imports_and_exports_text()
    {
        var client = CreateClient();
        var imported = await Read<Diagram>(
            await client.PostAsJsonAsync("/api/diagrams/import", new { text = "@startuml\ntitle Zoo\nCat --> Animal : is a\n@enduml" }),
            HttpStatusCode.Created);

        var mermaid = await client.GetStringAsync($"/api/diagrams/{imported.Id}/export?format=mermaid");

        Assert.Equal("Zoo", imported.Name);
        Assert.StartsWith(
            "classDiagram\n  class Cat[\"Cat\"]\n  class Animal[\"Animal\"]\n  Cat --> Animal : is a\n  %% kandinsky:layout Cat ",
            mermaid);
    }

    [Theory]
    [InlineData("classDiagram\n  A --> B", "shop", "shop")]
    [InlineData("---\ntitle: Zoo\n---\nclassDiagram\n  A --> B", "shop", "Zoo")]
    [InlineData("classDiagram\n  A --> B", null, "Imported diagram")]
    [InlineData("classDiagram\n  A --> B", "  ", "Imported diagram")]
    public async Task Import_names_the_diagram_after_its_title_else_the_fallback_name(string text, string? fallbackName, string expected)
    {
        var client = CreateClient();

        var imported = await Read<Diagram>(
            await client.PostAsJsonAsync("/api/diagrams/import", new { text, fallbackName }),
            HttpStatusCode.Created);

        Assert.Equal(expected, imported.Name);
    }

    [Fact]
    public async Task Import_without_layout_applies_auto_layout()
    {
        var client = CreateClient();

        var imported = await Read<Diagram>(
            await client.PostAsJsonAsync("/api/diagrams/import", new { text = "classDiagram\n  C --> B\n  B --> A\n  class Lonely" }),
            HttpStatusCode.Created);

        var y = imported.Shapes.ToDictionary(s => s.Id, s => s.Y);
        Assert.True(y["C"] < y["B"] && y["B"] < y["A"] && y["A"] < y["Lonely"]);
    }

    [Theory]
    [InlineData("plantuml")]
    [InlineData("mermaid")]
    public async Task Export_and_import_keep_the_layout(string format)
    {
        var client = CreateClient();
        var diagram = await Read<Diagram>(await client.PostAsJsonAsync("/api/diagrams", new { name = "Layout" }), HttpStatusCode.Created);
        var a = await Read<Shape>(await client.PostAsJsonAsync($"/api/diagrams/{diagram.Id}/shapes", new { label = "A", x = 500, y = 30, width = 180, height = 90 }), HttpStatusCode.Created);
        var b = await Read<Shape>(await client.PostAsJsonAsync($"/api/diagrams/{diagram.Id}/shapes", new { label = "B", x = 20.5, y = 400 }), HttpStatusCode.Created);
        await client.PostAsJsonAsync($"/api/diagrams/{diagram.Id}/edges", new { sourceId = a.Id, targetId = b.Id });

        var text = await client.GetStringAsync($"/api/diagrams/{diagram.Id}/export?format={format}");
        var imported = await Read<Diagram>(await client.PostAsJsonAsync("/api/diagrams/import", new { text }), HttpStatusCode.Created);

        Assert.Equal([a, b], imported.Shapes);
    }

    [Fact]
    public async Task Import_places_shapes_without_layout_in_free_slots()
    {
        var client = CreateClient();
        var text = "@startuml\nA --> B\n' kandinsky:layout A x=40 y=40 width=140 height=60\n@enduml";

        var imported = await Read<Diagram>(await client.PostAsJsonAsync("/api/diagrams/import", new { text }), HttpStatusCode.Created);

        Assert.Equal(new Shape("A", "A", 40, 40, 140, 60), imported.Shapes[0]);
        Assert.Equal((240.0, 40.0), (imported.Shapes[1].X, imported.Shapes[1].Y));
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

    [Fact]
    public async Task Paste_adds_shapes_and_edges_centered_on_the_given_point()
    {
        var client = CreateClient();
        var diagram = await Read<Diagram>(await client.PostAsJsonAsync("/api/diagrams", new { }), HttpStatusCode.Created);
        var existing = await Read<Shape>(await client.PostAsJsonAsync($"/api/diagrams/{diagram.Id}/shapes", new { label = "Old", x = 0, y = 0 }), HttpStatusCode.Created);
        var text = "classDiagram\n  A --> B : uses\n  %% kandinsky:layout A x=40 y=40 width=140 height=60\n  %% kandinsky:layout B x=40 y=200 width=140 height=60";

        var pasted = await Read<PasteResult>(
            await client.PostAsJsonAsync($"/api/diagrams/{diagram.Id}/paste", new { text, centerX = 500, centerY = 400 }),
            HttpStatusCode.OK);

        Assert.Equal(
            [existing, new Shape("A", "A", 430, 290, 140, 60), new Shape("B", "B", 430, 450, 140, 60)],
            pasted.Diagram.Shapes);
        var edge = Assert.Single(pasted.Diagram.Edges);
        Assert.Equal(("A", "B", "uses"), (edge.SourceId, edge.TargetId, edge.Label));
        Assert.Equal(["A", "B"], pasted.ShapeIds);
        Assert.Equal([edge.Id], pasted.EdgeIds);
        Assert.Equal(pasted.Diagram, await client.GetFromJsonAsync<Diagram>($"/api/diagrams/{diagram.Id}"), DiagramComparer);
    }

    [Fact]
    public async Task Paste_renames_ids_already_used_and_places_below_without_center()
    {
        var client = CreateClient();
        var diagram = await Read<Diagram>(
            await client.PostAsJsonAsync("/api/diagrams/import", new { text = "@startuml\nA --> B\n@enduml" }),
            HttpStatusCode.Created);
        var bottom = diagram.Shapes.Max(s => s.Y + s.Height);

        var pasted = await Read<PasteResult>(
            await client.PostAsJsonAsync($"/api/diagrams/{diagram.Id}/paste", new { text = "@startuml\nA --> C\n@enduml" }),
            HttpStatusCode.OK);

        Assert.Equal(4, pasted.Diagram.Shapes.Count);
        var added = pasted.Diagram.Shapes.Where(s => pasted.ShapeIds.Contains(s.Id)).ToList();
        Assert.Equal(["A", "C"], added.Select(s => s.Label));
        Assert.DoesNotContain("A", pasted.ShapeIds);
        Assert.Equal(bottom + 80, added.Min(s => s.Y));
        var edge = pasted.Diagram.Edges.Single(e => pasted.EdgeIds.Contains(e.Id));
        Assert.Equal((pasted.ShapeIds[0], pasted.ShapeIds[1]), (edge.SourceId, edge.TargetId));
        Assert.Equal(2, pasted.Diagram.Edges.Select(e => e.Id).Distinct().Count());
    }

    [Fact]
    public async Task Paste_accepts_a_mermaid_flowchart_in_a_markdown_code_fence()
    {
        var client = CreateClient();
        var diagram = await Read<Diagram>(await client.PostAsJsonAsync("/api/diagrams", new { }), HttpStatusCode.Created);
        var text = "```mermaid\nflowchart LR\n    MXO[MXO REST API] --> DS[DataSource Backend<br/>CronJob]\n    DS -->|gRPC bulk| ASSET[Asset.API]\n```\n";

        var pasted = await Read<PasteResult>(
            await client.PostAsJsonAsync($"/api/diagrams/{diagram.Id}/paste", new { text, centerX = 400, centerY = 300 }),
            HttpStatusCode.OK);

        Assert.Equal(["MXO REST API", "DataSource Backend CronJob", "Asset.API"], pasted.Diagram.Shapes.Select(s => s.Label));
        Assert.Equal([null, "gRPC bulk"], pasted.Diagram.Edges.Select(e => e.Label));
    }

    [Theory]
    [InlineData("\uFEFF```mermaid\r\nflowchart LR\r\n    A[One] --> B[Two]\r\n```")]
    [InlineData("```mermaid\rflowchart\u00A0LR\r\u200BA[One]\u00A0-->\u00A0B[Two]\r```")]
    [InlineData("```mermaid\u2028flowchart LR\u2028A[One] --> B[Two]\u2028```")]
    public async Task Paste_accepts_text_with_line_breaks_and_invisible_characters_from_other_apps(string text)
    {
        var client = CreateClient();
        var diagram = await Read<Diagram>(await client.PostAsJsonAsync("/api/diagrams", new { }), HttpStatusCode.Created);

        var pasted = await Read<PasteResult>(
            await client.PostAsJsonAsync($"/api/diagrams/{diagram.Id}/paste", new { text, centerX = 400, centerY = 300 }),
            HttpStatusCode.OK);

        Assert.Equal(["One", "Two"], pasted.Diagram.Shapes.Select(s => s.Label));
        Assert.Single(pasted.Diagram.Edges);
    }

    [Theory]
    [InlineData("just some text", "Cannot detect the format of 'just some text'. Specify 'plantuml' or 'mermaid'.")]
    [InlineData("```mermaid\nsequenceDiagram\n  A->>B: hi\n```", "Line 2: expected 'classDiagram' or 'flowchart'. Only Mermaid class diagrams and flowcharts are supported.")]
    [InlineData("classDiagram\n  A -- B", "Line 2: unsupported syntax 'A -- B'.")]
    [InlineData("classDiagram", "The pasted text contains no classes.")]
    public async Task Paste_reports_invalid_text_and_keeps_the_diagram(string text, string expected)
    {
        var client = CreateClient();
        var diagram = await Read<Diagram>(await client.PostAsJsonAsync("/api/diagrams", new { }), HttpStatusCode.Created);

        var problem = await Read<ProblemDetails>(
            await client.PostAsJsonAsync($"/api/diagrams/{diagram.Id}/paste", new { text, centerX = 100, centerY = 100 }),
            HttpStatusCode.BadRequest);

        Assert.Equal(expected, problem.Detail);
        Assert.Empty((await client.GetFromJsonAsync<Diagram>($"/api/diagrams/{diagram.Id}"))!.Shapes);
    }
}
