using Kandinsky.Server.Formats;
using Kandinsky.Server.Model;

namespace Kandinsky.Server.Diagrams;

/// <summary>Owns the diagram state. All changes to diagrams go through this service.</summary>
public sealed class DiagramService
{
    private const string UntitledName = "Untitled";
    private const string ImportedName = "Imported diagram";

    private readonly Lock _lock = new();
    private readonly Dictionary<string, Diagram> _diagrams;
    private readonly DiagramFileStore _files;

    public DiagramService(DiagramFileStore files)
    {
        _files = files;
        _diagrams = files.LoadAll().Select(WithParents).ToDictionary(d => d.Id);
    }

    public IReadOnlyList<DiagramSummary> List()
    {
        lock (_lock)
        {
            return _diagrams.Values
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .Select(d => new DiagramSummary(d.Id, d.Name))
                .ToList();
        }
    }

    public Diagram Get(string id)
    {
        lock (_lock)
        {
            return Find(id);
        }
    }

    public Diagram Create(string? name)
    {
        var diagram = new Diagram(NewDiagramId(), DiagramInput.ValidName(name ?? UntitledName), [], []);
        lock (_lock)
        {
            return Store(diagram);
        }
    }

    public Diagram Rename(string id, string name)
    {
        lock (_lock)
        {
            return Store(Find(id) with { Name = DiagramInput.ValidName(name) });
        }
    }

    public void Delete(string id)
    {
        lock (_lock)
        {
            Find(id);
            _files.Delete(id);
            _diagrams.Remove(id);
        }
    }

    public void Save(string id)
    {
        lock (_lock)
        {
            _files.Save(Find(id));
        }
    }

    public Shape GetShape(string diagramId, string shapeId)
    {
        lock (_lock)
        {
            return FindShape(Find(diagramId), shapeId);
        }
    }

    public Shape AddShape(string diagramId, CreateShapeRequest request)
    {
        lock (_lock)
        {
            var diagram = Find(diagramId);
            var shape = NewShape(diagram, request);
            return FindShape(Store(diagram with { Shapes = [.. diagram.Shapes, shape] }), shape.Id);
        }
    }

    public Shape UpdateShape(string diagramId, string shapeId, UpdateShapeRequest request)
    {
        lock (_lock)
        {
            var diagram = Find(diagramId);
            var updated = Updated(FindShape(diagram, shapeId), request);
            var shapes = diagram.Shapes.Select(s => s.Id == shapeId ? updated : s).ToList();
            // The parent group may change with the new bounds, so the stored shape is returned.
            return FindShape(Store(diagram with { Shapes = shapes }), shapeId);
        }
    }

    public void DeleteShape(string diagramId, string shapeId)
    {
        lock (_lock)
        {
            var diagram = Find(diagramId);
            FindShape(diagram, shapeId);
            // A group is deleted with everything inside it.
            var deletedIds = DiagramGroups.Descendants(diagram.Shapes, [shapeId]);
            deletedIds.Add(shapeId);
            Store(diagram with
            {
                Shapes = diagram.Shapes.Where(s => !deletedIds.Contains(s.Id)).ToList(),
                Edges = diagram.Edges.Where(e => !deletedIds.Contains(e.SourceId) && !deletedIds.Contains(e.TargetId)).ToList(),
            });
        }
    }

    public Edge GetEdge(string diagramId, string edgeId)
    {
        lock (_lock)
        {
            return FindEdge(Find(diagramId), edgeId);
        }
    }

    public Edge AddEdge(string diagramId, CreateEdgeRequest request)
    {
        lock (_lock)
        {
            var diagram = Find(diagramId);
            DiagramInput.ValidateEndpoints(diagram, request.SourceId, request.TargetId);
            var edge = new Edge(
                ElementIdAllocator.For(diagram).NextEdgeId(),
                request.SourceId,
                request.TargetId,
                DiagramInput.ValidEdgeLabel(request.Label),
                request.Routing ?? EdgeRouting.Straight,
                request.SourceCorner ?? EdgeCorner.Auto,
                request.TargetCorner ?? EdgeCorner.Auto);
            Store(diagram with { Edges = [.. diagram.Edges, edge] });
            return edge;
        }
    }

    public Edge UpdateEdge(string diagramId, string edgeId, UpdateEdgeRequest request)
    {
        lock (_lock)
        {
            var diagram = Find(diagramId);
            var updated = Updated(FindEdge(diagram, edgeId), request);
            DiagramInput.ValidateEndpoints(diagram, updated.SourceId, updated.TargetId);
            Store(diagram with { Edges = diagram.Edges.Select(e => e.Id == edgeId ? updated : e).ToList() });
            return updated;
        }
    }

    public void DeleteEdge(string diagramId, string edgeId)
    {
        lock (_lock)
        {
            var diagram = Find(diagramId);
            FindEdge(diagram, edgeId);
            Store(diagram with { Edges = diagram.Edges.Where(e => e.Id != edgeId).ToList() });
        }
    }

    public string Export(string id, DiagramFormat format) => DiagramFormats.Export(Get(id), format);

    public Diagram Align(string diagramId, AlignShapesRequest request)
    {
        var shapeIds = (request.ShapeIds ?? []).Distinct().ToList();
        if (shapeIds.Count < 2)
        {
            throw new DiagramValidationException("Select at least two shapes to align.");
        }
        if (request.Direction is not { } direction)
        {
            throw new DiagramValidationException("Direction is required: 'horizontal' or 'vertical'.");
        }

        lock (_lock)
        {
            var diagram = Find(diagramId);
            DiagramInput.EnsureShapesExist(diagram, shapeIds);
            return Store(diagram with { Shapes = ShapeAligner.Align(diagram.Shapes, shapeIds, direction) });
        }
    }

    public Diagram AutoLayout(string diagramId)
    {
        lock (_lock)
        {
            var diagram = Find(diagramId);
            return Store(diagram with { Shapes = DiagramLayout.Arrange(diagram.Shapes, diagram.Edges) });
        }
    }

    public Diagram Import(ImportDiagramRequest request)
    {
        var imported = TextDiagramReader.Read(request.Text, request.Format);
        var name = request.Name ?? imported.Title ?? NonBlank(request.FallbackName) ?? ImportedName;
        var diagram = imported.Diagram with { Id = NewDiagramId(), Name = DiagramInput.ValidName(name) };
        lock (_lock)
        {
            return Store(diagram);
        }
    }

    public PasteResult Paste(string diagramId, PasteDiagramRequest request)
    {
        if (request.CenterX is null != request.CenterY is null)
        {
            throw new DiagramValidationException("CenterX and CenterY must be given together.");
        }
        var pasted = TextDiagramReader.Read(request.Text, request.Format).Diagram;
        if (pasted.Shapes.Count == 0)
        {
            throw new DiagramValidationException("The pasted text contains no classes.");
        }

        lock (_lock)
        {
            var result = DiagramPaste.Merge(Find(diagramId), pasted, request.CenterX, request.CenterY);
            return result with { Diagram = Store(result.Diagram) };
        }
    }

    private static Shape NewShape(Diagram diagram, CreateShapeRequest request)
    {
        var kind = request.Kind ?? ShapeKind.Rectangle;
        var (defaultWidth, defaultHeight) = ShapeDefaults.Size(kind);
        var width = DiagramInput.ValidSize(request.Width ?? defaultWidth, "Width");
        var height = DiagramInput.ValidSize(request.Height ?? defaultHeight, "Height");
        var (x, y) = request.X is null || request.Y is null
            ? FreeGridSlot.Find(diagram.Shapes, width, height)
            : (DiagramInput.ValidCoordinate(request.X.Value, "X"), DiagramInput.ValidCoordinate(request.Y.Value, "Y"));
        var label = DiagramInput.ValidLabel(request.Label ?? ShapeDefaults.Label(kind));
        return new Shape(ElementIdAllocator.For(diagram).NextShapeId(), label, x, y, width, height, kind);
    }

    private static Shape Updated(Shape shape, UpdateShapeRequest request) => shape with
    {
        Label = request.Label is null ? shape.Label : DiagramInput.ValidLabel(request.Label),
        X = request.X is null ? shape.X : DiagramInput.ValidCoordinate(request.X.Value, "X"),
        Y = request.Y is null ? shape.Y : DiagramInput.ValidCoordinate(request.Y.Value, "Y"),
        Width = request.Width is null ? shape.Width : DiagramInput.ValidSize(request.Width.Value, "Width"),
        Height = request.Height is null ? shape.Height : DiagramInput.ValidSize(request.Height.Value, "Height"),
        Kind = request.Kind ?? shape.Kind,
    };

    /// <summary>A reconnected end without a requested corner is released to auto: the old corner was chosen for the old shape.</summary>
    private static Edge Updated(Edge edge, UpdateEdgeRequest request)
    {
        var sourceMoved = request.SourceId is not null && request.SourceId != edge.SourceId;
        var targetMoved = request.TargetId is not null && request.TargetId != edge.TargetId;
        return edge with
        {
            SourceId = request.SourceId ?? edge.SourceId,
            TargetId = request.TargetId ?? edge.TargetId,
            Label = request.Label is null ? edge.Label : DiagramInput.ValidEdgeLabel(request.Label),
            Routing = request.Routing ?? edge.Routing,
            SourceCorner = request.SourceCorner ?? (sourceMoved ? EdgeCorner.Auto : edge.SourceCorner),
            TargetCorner = request.TargetCorner ?? (targetMoved ? EdgeCorner.Auto : edge.TargetCorner),
        };
    }

    /// <summary>Registers the diagram with its group membership updated to the current positions.</summary>
    private Diagram Store(Diagram diagram)
    {
        diagram = WithParents(diagram);
        _diagrams[diagram.Id] = diagram;
        return diagram;
    }

    private static Diagram WithParents(Diagram diagram) => diagram with { Shapes = DiagramGroups.AssignParents(diagram.Shapes) };

    private Diagram Find(string id) =>
        _diagrams.TryGetValue(id, out var diagram) ? diagram : throw new NotFoundException($"Diagram '{id}' not found.");

    private static Shape FindShape(Diagram diagram, string shapeId) =>
        diagram.Shapes.FirstOrDefault(s => s.Id == shapeId) ?? throw new NotFoundException($"Shape '{shapeId}' not found.");

    private static Edge FindEdge(Diagram diagram, string edgeId) =>
        diagram.Edges.FirstOrDefault(e => e.Id == edgeId) ?? throw new NotFoundException($"Edge '{edgeId}' not found.");

    private static string NewDiagramId() => Guid.NewGuid().ToString("N");

    private static string? NonBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
