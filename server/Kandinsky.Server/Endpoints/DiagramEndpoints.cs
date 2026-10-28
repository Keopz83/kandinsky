using Kandinsky.Server.Diagrams;
using Kandinsky.Server.Formats;
using Kandinsky.Server.Model;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Kandinsky.Server.Endpoints;

public static class DiagramEndpoints
{
    private const string BasePath = "/api/diagrams";
    private const string PlainTextUtf8 = "text/plain; charset=utf-8";

    public static void MapDiagramEndpoints(this WebApplication app)
    {
        var diagrams = app.MapGroup(BasePath)
            .WithTags("Diagrams")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        MapDiagrams(diagrams);
        MapTextFormats(diagrams);
        MapLayout(diagrams);
        MapShapes(diagrams.MapGroup("/{id}/shapes").WithTags("Shapes"));
        MapEdges(diagrams.MapGroup("/{id}/edges").WithTags("Edges"));
    }

    private static void MapDiagrams(RouteGroupBuilder diagrams)
    {
        diagrams.MapGet("/", (DiagramService service) => TypedResults.Ok(service.List()))
            .WithName("ListDiagrams")
            .WithSummary("List all diagrams");

        diagrams.MapPost("/", ([FromBody] CreateDiagramRequest? request, DiagramService service) =>
            {
                var diagram = service.Create(request?.Name);
                return TypedResults.Created($"{BasePath}/{diagram.Id}", diagram);
            })
            .WithName("CreateDiagram")
            .WithSummary("Create an empty diagram");

        diagrams.MapGet("/{id}", (string id, DiagramService service) => TypedResults.Ok(service.Get(id)))
            .WithName("GetDiagram")
            .WithSummary("Get a diagram with all shapes and edges");

        diagrams.MapPatch("/{id}", (string id, UpdateDiagramRequest request, DiagramService service) => TypedResults.Ok(service.Rename(id, request.Name)))
            .WithName("RenameDiagram")
            .WithSummary("Rename a diagram");

        diagrams.MapDelete("/{id}", (string id, DiagramService service) =>
            {
                service.Delete(id);
                return TypedResults.NoContent();
            })
            .WithName("DeleteDiagram")
            .WithSummary("Delete a diagram, including its saved file");

        diagrams.MapPost("/{id}/save", (string id, DiagramService service) =>
            {
                service.Save(id);
                return TypedResults.NoContent();
            })
            .WithName("SaveDiagram")
            .WithSummary("Persist the current state of a diagram on the server");
    }

    private static void MapTextFormats(RouteGroupBuilder diagrams)
    {
        diagrams.MapGet("/{id}/export", (string id, string format, DiagramService service) =>
                TypedResults.Text(service.Export(id, DiagramFormats.Parse(format)), PlainTextUtf8))
            .WithName("ExportDiagram")
            .WithSummary("Export a diagram as PlantUML or Mermaid text")
            .WithDescription("Query parameter `format` is `plantuml` or `mermaid`.")
            .Produces<string>(StatusCodes.Status200OK, "text/plain");

        diagrams.MapPost("/import", (ImportDiagramRequest request, DiagramService service) =>
            {
                var diagram = service.Import(request);
                return TypedResults.Created($"{BasePath}/{diagram.Id}", diagram);
            })
            .WithName("ImportDiagram")
            .WithSummary("Create a diagram from PlantUML or Mermaid text")
            .WithDescription("Supports class diagrams with classes and directed (optionally labeled) associations, PlantUML `database`, `hexagon` (decision), other deployment elements for flowchart symbols and container elements such as `package` (group), and Mermaid flowcharts in classic or v11 shape syntax (`[(text)]` nodes become databases, `{text}` nodes decisions, `([text])` terminals, `A@{ shape: doc }` documents, …; subgraphs become groups). `skinparam linetype ortho` and a Mermaid `curve: step` setting make all edges angled. Shapes are laid out in a grid. The diagram is named `name`, else the title in the text, else `fallbackName` (e.g. the file name).");

        diagrams.MapPost("/{id}/paste", (string id, PasteDiagramRequest request, DiagramService service) => TypedResults.Ok(service.Paste(id, request)))
            .WithName("PasteDiagram")
            .WithSummary("Add the classes and associations of PlantUML or Mermaid text to a diagram")
            .WithDescription("The pasted shapes keep their relative layout (layout comments, else auto-layout) and are centered on `centerX`/`centerY`, else placed below the existing shapes. Ids already used in the diagram are replaced. Returns the updated diagram and the ids of the added shapes and edges.");
    }

    private static void MapLayout(RouteGroupBuilder diagrams)
    {
        diagrams.MapPost("/{id}/align", (string id, AlignShapesRequest request, DiagramService service) => TypedResults.Ok(service.Align(id, request)))
            .WithTags("Layout")
            .WithName("AlignShapes")
            .WithSummary("Align shapes in a row or column")
            .WithDescription("`horizontal`: vertical centers match the first shape's. `vertical`: horizontal centers match the first shape's. Returns the updated diagram.");

        diagrams.MapPost("/{id}/layout", (string id, DiagramService service) => TypedResults.Ok(service.AutoLayout(id)))
            .WithTags("Layout")
            .WithName("AutoLayout")
            .WithSummary("Arrange all shapes automatically")
            .WithDescription("Connected shapes are placed in layers following the edge direction, unconnected shapes in a grid below. Returns the updated diagram.");
    }

    private static void MapShapes(RouteGroupBuilder shapes)
    {
        shapes.MapPost("/", (string id, CreateShapeRequest request, DiagramService service) =>
            {
                var shape = service.AddShape(id, request);
                return TypedResults.Created($"{BasePath}/{id}/shapes/{shape.Id}", shape);
            })
            .WithName("AddShape")
            .WithSummary("Add a shape")
            .WithDescription("`kind` is `rectangle` (default), `database`, `decision`, `group` or a flowchart symbol: `start`, `end`, `terminal`, `rounded`, `io`, `subroutine`, `document`, `connector`, `preparation`, `manual`. Without X and Y the server places the shape in a free spot. A shape lying fully inside a group belongs to it (`parentId`, derived from the positions).");

        shapes.MapGet("/{shapeId}", (string id, string shapeId, DiagramService service) => TypedResults.Ok(service.GetShape(id, shapeId)))
            .WithName("GetShape")
            .WithSummary("Get a shape");

        shapes.MapPatch("/{shapeId}", (string id, string shapeId, UpdateShapeRequest request, DiagramService service) =>
                TypedResults.Ok(service.UpdateShape(id, shapeId, request)))
            .WithName("UpdateShape")
            .WithSummary("Move, resize, relabel or change the kind of a shape");

        shapes.MapDelete("/{shapeId}", (string id, string shapeId, DiagramService service) =>
            {
                service.DeleteShape(id, shapeId);
                return TypedResults.NoContent();
            })
            .WithName("DeleteShape")
            .WithSummary("Delete a shape and its connected edges; a group is deleted with everything inside it");
    }

    private static void MapEdges(RouteGroupBuilder edges)
    {
        edges.MapPost("/", (string id, CreateEdgeRequest request, DiagramService service) =>
            {
                var edge = service.AddEdge(id, request);
                return TypedResults.Created($"{BasePath}/{id}/edges/{edge.Id}", edge);
            })
            .WithName("AddEdge")
            .WithSummary("Connect two shapes with a directed edge")
            .WithDescription(
                "`routing` is `straight` (default) or `angled` (horizontal and vertical segments). "
                + "`sourceCorner` and `targetCorner` (`auto` by default, `top`, `right`, `bottom`, `left`) fix the corner of a decision diamond the edge is attached to.");

        edges.MapGet("/{edgeId}", (string id, string edgeId, DiagramService service) => TypedResults.Ok(service.GetEdge(id, edgeId)))
            .WithName("GetEdge")
            .WithSummary("Get an edge");

        edges.MapPatch("/{edgeId}", (string id, string edgeId, UpdateEdgeRequest request, DiagramService service) =>
                TypedResults.Ok(service.UpdateEdge(id, edgeId, request)))
            .WithName("UpdateEdge")
            .WithSummary("Relabel or reconnect an edge");

        edges.MapDelete("/{edgeId}", (string id, string edgeId, DiagramService service) =>
            {
                service.DeleteEdge(id, edgeId);
                return TypedResults.NoContent();
            })
            .WithName("DeleteEdge")
            .WithSummary("Delete an edge");
    }
}
