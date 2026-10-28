using Kandinsky.Server.Diagrams;
using Kandinsky.Server.Formats;
using Kandinsky.Server.Model;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Kandinsky.Server.Endpoints;

public static class DiagramEndpoints
{
    public static void MapDiagramEndpoints(this WebApplication app)
    {
        var diagrams = app.MapGroup("/api/diagrams")
            .WithTags("Diagrams")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        diagrams.MapGet("/", (DiagramService service) => TypedResults.Ok(service.List()))
            .WithName("ListDiagrams")
            .WithSummary("List all diagrams");

        diagrams.MapPost("/", ([FromBody] CreateDiagramRequest? request, DiagramService service) =>
            {
                var diagram = service.Create(request?.Name);
                return TypedResults.Created($"/api/diagrams/{diagram.Id}", diagram);
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

        diagrams.MapGet("/{id}/export", (string id, string format, DiagramService service) =>
                TypedResults.Text(service.Export(id, DiagramFormats.Parse(format)), "text/plain; charset=utf-8"))
            .WithName("ExportDiagram")
            .WithSummary("Export a diagram as PlantUML or Mermaid text")
            .WithDescription("Query parameter `format` is `plantuml` or `mermaid`.")
            .Produces<string>(StatusCodes.Status200OK, "text/plain");

        diagrams.MapPost("/import", (ImportDiagramRequest request, DiagramService service) =>
            {
                var diagram = service.Import(request);
                return TypedResults.Created($"/api/diagrams/{diagram.Id}", diagram);
            })
            .WithName("ImportDiagram")
            .WithSummary("Create a diagram from PlantUML or Mermaid text")
            .WithDescription("Supports class diagrams with classes and directed associations, PlantUML `database` elements and Mermaid flowcharts (`[(text)]` nodes become databases). Shapes are laid out in a grid. The diagram is named `name`, else the title in the text, else `fallbackName` (e.g. the file name).");

        diagrams.MapPost("/{id}/paste", (string id, PasteDiagramRequest request, DiagramService service) => TypedResults.Ok(service.Paste(id, request)))
            .WithName("PasteDiagram")
            .WithSummary("Add the classes and associations of PlantUML or Mermaid text to a diagram")
            .WithDescription("The pasted shapes keep their relative layout (layout comments, else auto-layout) and are centered on `centerX`/`centerY`, else placed below the existing shapes. Ids already used in the diagram are replaced. Returns the updated diagram and the ids of the added shapes and edges.");

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

        var shapes = diagrams.MapGroup("/{id}/shapes").WithTags("Shapes");

        shapes.MapPost("/", (string id, CreateShapeRequest request, DiagramService service) =>
            {
                var shape = service.AddShape(id, request);
                return TypedResults.Created($"/api/diagrams/{id}/shapes/{shape.Id}", shape);
            })
            .WithName("AddShape")
            .WithSummary("Add a shape")
            .WithDescription("`kind` is `rectangle` (default) or `database`. Without X and Y the server places the shape in a free spot.");

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
            .WithSummary("Delete a rectangle and its connected edges");

        var edges = diagrams.MapGroup("/{id}/edges").WithTags("Edges");

        edges.MapPost("/", (string id, CreateEdgeRequest request, DiagramService service) =>
            {
                var edge = service.AddEdge(id, request);
                return TypedResults.Created($"/api/diagrams/{id}/edges/{edge.Id}", edge);
            })
            .WithName("AddEdge")
            .WithSummary("Connect two rectangles with a directed edge");

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
