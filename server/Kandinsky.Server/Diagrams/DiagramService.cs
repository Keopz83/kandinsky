using Kandinsky.Server.Formats;
using Kandinsky.Server.Model;

namespace Kandinsky.Server.Diagrams;

/// <summary>Owns the diagram state. All changes to diagrams go through this service.</summary>
public sealed class DiagramService
{
    public const double DefaultWidth = 140;
    public const double DefaultHeight = 60;
    private const double MinSize = 20;
    private const double MaxCoordinate = 100_000;
    private const int MaxTextLength = 200;
    private const int MaxImportLength = 1_000_000;
    private const int GridColumns = 5;
    private const double GridX = 200;
    private const double GridY = 120;
    private const double GridOrigin = 40;
    private const double PasteGap = 80;

    private readonly Lock _lock = new();
    private readonly Dictionary<string, Diagram> _diagrams;
    private readonly DiagramFileStore _files;

    public DiagramService(DiagramFileStore files)
    {
        _files = files;
        _diagrams = files.LoadAll().ToDictionary(d => d.Id);
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
        var diagram = new Diagram(NewDiagramId(), ValidName(name ?? "Untitled"), [], []);
        lock (_lock)
        {
            _diagrams[diagram.Id] = diagram;
        }
        return diagram;
    }

    public Diagram Rename(string id, string name)
    {
        lock (_lock)
        {
            var diagram = Find(id) with { Name = ValidName(name) };
            _diagrams[id] = diagram;
            return diagram;
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
            var width = ValidSize(request.Width ?? DefaultWidth, "Width");
            var height = ValidSize(request.Height ?? DefaultHeight, "Height");
            var (x, y) = request.X is null || request.Y is null
                ? FreePosition(diagram, width, height)
                : (ValidCoordinate(request.X.Value, "X"), ValidCoordinate(request.Y.Value, "Y"));
            var kind = request.Kind ?? ShapeKind.Rectangle;
            var label = request.Label ?? (kind == ShapeKind.Database ? "Database" : "Class");
            var shape = new Shape(NextId(diagram, "s"), ValidLabel(label), x, y, width, height, kind);
            _diagrams[diagramId] = diagram with { Shapes = [.. diagram.Shapes, shape] };
            return shape;
        }
    }

    public Shape UpdateShape(string diagramId, string shapeId, UpdateShapeRequest request)
    {
        lock (_lock)
        {
            var diagram = Find(diagramId);
            var shape = FindShape(diagram, shapeId);
            var updated = shape with
            {
                Label = request.Label is null ? shape.Label : ValidLabel(request.Label),
                X = request.X is null ? shape.X : ValidCoordinate(request.X.Value, "X"),
                Y = request.Y is null ? shape.Y : ValidCoordinate(request.Y.Value, "Y"),
                Width = request.Width is null ? shape.Width : ValidSize(request.Width.Value, "Width"),
                Height = request.Height is null ? shape.Height : ValidSize(request.Height.Value, "Height"),
                Kind = request.Kind ?? shape.Kind,
            };
            _diagrams[diagramId] = diagram with { Shapes = diagram.Shapes.Select(s => s.Id == shapeId ? updated : s).ToList() };
            return updated;
        }
    }

    public void DeleteShape(string diagramId, string shapeId)
    {
        lock (_lock)
        {
            var diagram = Find(diagramId);
            FindShape(diagram, shapeId);
            _diagrams[diagramId] = diagram with
            {
                Shapes = diagram.Shapes.Where(s => s.Id != shapeId).ToList(),
                Edges = diagram.Edges.Where(e => e.SourceId != shapeId && e.TargetId != shapeId).ToList(),
            };
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
            ValidateEndpoints(diagram, request.SourceId, request.TargetId);
            var edge = new Edge(NextId(diagram, "e"), request.SourceId, request.TargetId, ValidEdgeLabel(request.Label));
            _diagrams[diagramId] = diagram with { Edges = [.. diagram.Edges, edge] };
            return edge;
        }
    }

    public Edge UpdateEdge(string diagramId, string edgeId, UpdateEdgeRequest request)
    {
        lock (_lock)
        {
            var diagram = Find(diagramId);
            var edge = FindEdge(diagram, edgeId);
            var updated = edge with
            {
                SourceId = request.SourceId ?? edge.SourceId,
                TargetId = request.TargetId ?? edge.TargetId,
                Label = request.Label is null ? edge.Label : ValidEdgeLabel(request.Label),
            };
            ValidateEndpoints(diagram, updated.SourceId, updated.TargetId);
            _diagrams[diagramId] = diagram with { Edges = diagram.Edges.Select(e => e.Id == edgeId ? updated : e).ToList() };
            return updated;
        }
    }

    public void DeleteEdge(string diagramId, string edgeId)
    {
        lock (_lock)
        {
            var diagram = Find(diagramId);
            FindEdge(diagram, edgeId);
            _diagrams[diagramId] = diagram with { Edges = diagram.Edges.Where(e => e.Id != edgeId).ToList() };
        }
    }

    public string Export(string id, DiagramFormat format) => DiagramFormats.Export(Get(id), format);

    public Diagram Align(string diagramId, AlignShapesRequest request)
    {
        var ids = (request.ShapeIds ?? []).Distinct().ToList();
        if (ids.Count < 2)
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
            var missing = ids.FirstOrDefault(id => diagram.Shapes.All(s => s.Id != id));
            if (missing is not null)
            {
                throw new DiagramValidationException($"Shape '{missing}' does not exist in this diagram.");
            }

            var anchor = diagram.Shapes.First(s => s.Id == ids[0]);
            var selected = ids.ToHashSet();
            var shapes = diagram.Shapes
                .Select(s => !selected.Contains(s.Id) ? s : direction == AlignDirection.Horizontal
                    ? s with { Y = Math.Max(0, anchor.Y + anchor.Height / 2 - s.Height / 2) }
                    : s with { X = Math.Max(0, anchor.X + anchor.Width / 2 - s.Width / 2) })
                .ToList();
            diagram = diagram with { Shapes = shapes };
            _diagrams[diagramId] = diagram;
            return diagram;
        }
    }

    public Diagram AutoLayout(string diagramId)
    {
        lock (_lock)
        {
            var diagram = Find(diagramId);
            diagram = diagram with { Shapes = DiagramLayout.Arrange(diagram.Shapes, diagram.Edges) };
            _diagrams[diagramId] = diagram;
            return diagram;
        }
    }

    public Diagram Import(ImportDiagramRequest request)
    {
        var (parsed, diagram) = ParseText(request.Text, request.Format);
        diagram = diagram with { Id = NewDiagramId(), Name = ValidName(request.Name ?? parsed.Title ?? NonBlank(request.FallbackName) ?? "Imported diagram") };
        lock (_lock)
        {
            _diagrams[diagram.Id] = diagram;
        }
        return diagram;
    }

    public PasteResult Paste(string diagramId, PasteDiagramRequest request)
    {
        if (request.CenterX is null != request.CenterY is null)
        {
            throw new DiagramValidationException("CenterX and CenterY must be given together.");
        }
        var (_, pasted) = ParseText(request.Text, request.Format);
        if (pasted.Shapes.Count == 0)
        {
            throw new DiagramValidationException("The pasted text contains no classes.");
        }

        lock (_lock)
        {
            var diagram = Find(diagramId);
            var used = diagram.Shapes.Select(s => s.Id).Concat(diagram.Edges.Select(e => e.Id)).ToHashSet();
            // Pasted shapes are new shapes: ids already taken in the diagram are replaced.
            var ids = pasted.Shapes.ToDictionary(s => s.Id, s => used.Add(s.Id) ? s.Id : NextId(used, "s"));

            var left = pasted.Shapes.Min(s => s.X);
            var top = pasted.Shapes.Min(s => s.Y);
            var (dx, dy) = request.CenterX is { } cx && request.CenterY is { } cy
                ? (cx - (left + pasted.Shapes.Max(s => s.X + s.Width)) / 2, cy - (top + pasted.Shapes.Max(s => s.Y + s.Height)) / 2)
                : (0, diagram.Shapes.Count == 0 ? 0 : diagram.Shapes.Max(s => s.Y + s.Height) + PasteGap - top);
            dx = Math.Max(Math.Round(dx), -left);
            dy = Math.Max(Math.Round(dy), -top);

            var shapes = pasted.Shapes
                .Select(s => s with { Id = ids[s.Id], X = ValidCoordinate(s.X + dx, "X"), Y = ValidCoordinate(s.Y + dy, "Y") })
                .ToList();
            var edges = pasted.Edges
                .Select(e => e with { Id = NextId(used, "e"), SourceId = ids[e.SourceId], TargetId = ids[e.TargetId] })
                .ToList();
            diagram = diagram with { Shapes = [.. diagram.Shapes, .. shapes], Edges = [.. diagram.Edges, .. edges] };
            _diagrams[diagramId] = diagram;
            return new PasteResult(diagram, shapes.Select(s => s.Id).ToList(), edges.Select(e => e.Id).ToList());
        }
    }

    /// <summary>Parses PlantUML or Mermaid text into an unregistered diagram, laid out as on import (R-IO-7).</summary>
    private static (ParsedDiagram Parsed, Diagram Diagram) ParseText(string? text, string? formatName)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new DiagramValidationException("Import text must not be empty.");
        }
        if (text.Length > MaxImportLength)
        {
            throw new DiagramValidationException($"Import text must not exceed {MaxImportLength} characters.");
        }

        text = DiagramFormats.Unfence(DiagramFormats.Normalize(text));
        var format = formatName is null ? DiagramFormats.Detect(text) : DiagramFormats.Parse(formatName);
        var parsed = DiagramFormats.Parse(text, format);

        var used = parsed.Shapes.Select(s => s.Id).ToHashSet();
        var edges = parsed.Edges
            .Select(e => new Edge(NextId(used, "e"), e.SourceId, e.TargetId, ValidEdgeLabel(e.Label)))
            .ToList();
        var diagram = new Diagram("", "", [], edges);

        if (parsed.Shapes.All(s => s.Bounds is null))
        {
            var shapes = parsed.Shapes.Select(s => new Shape(s.Id, ValidLabel(s.Label), 0, 0, DefaultWidth, DefaultHeight, s.Kind)).ToList();
            diagram = diagram with { Shapes = DiagramLayout.Arrange(shapes, edges) };
        }
        else
        {
            // Shapes with a layout comment keep their position; shapes added without one go to free grid slots.
            var placed = parsed.Shapes
                .Where(s => s.Bounds is not null)
                .Select(s => new Shape(
                    s.Id,
                    ValidLabel(s.Label),
                    ValidCoordinate(s.Bounds!.X, "X"),
                    ValidCoordinate(s.Bounds.Y, "Y"),
                    ValidSize(s.Bounds.Width, "Width"),
                    ValidSize(s.Bounds.Height, "Height"),
                    s.Kind))
                .ToDictionary(s => s.Id);
            diagram = diagram with { Shapes = [.. placed.Values] };
            foreach (var s in parsed.Shapes.Where(s => s.Bounds is null))
            {
                var (x, y) = FreePosition(diagram, DefaultWidth, DefaultHeight);
                placed[s.Id] = new Shape(s.Id, ValidLabel(s.Label), x, y, DefaultWidth, DefaultHeight, s.Kind);
                diagram = diagram with { Shapes = [.. placed.Values] };
            }
            diagram = diagram with { Shapes = parsed.Shapes.Select(s => placed[s.Id]).ToList() };
        }
        return (parsed, diagram);
    }

    private Diagram Find(string id) =>
        _diagrams.TryGetValue(id, out var diagram) ? diagram : throw new NotFoundException($"Diagram '{id}' not found.");

    private static Shape FindShape(Diagram diagram, string shapeId) =>
        diagram.Shapes.FirstOrDefault(s => s.Id == shapeId) ?? throw new NotFoundException($"Shape '{shapeId}' not found.");

    private static Edge FindEdge(Diagram diagram, string edgeId) =>
        diagram.Edges.FirstOrDefault(e => e.Id == edgeId) ?? throw new NotFoundException($"Edge '{edgeId}' not found.");

    private static void ValidateEndpoints(Diagram diagram, string? sourceId, string? targetId)
    {
        if (string.IsNullOrEmpty(sourceId) || string.IsNullOrEmpty(targetId))
        {
            throw new DiagramValidationException("Edge source and target are required.");
        }
        if (sourceId == targetId)
        {
            throw new DiagramValidationException("Edge source and target must be different shapes.");
        }
        foreach (var id in new[] { sourceId, targetId })
        {
            if (diagram.Shapes.All(s => s.Id != id))
            {
                throw new DiagramValidationException($"Shape '{id}' does not exist in this diagram.");
            }
        }
    }

    private static (double X, double Y) FreePosition(Diagram diagram, double width, double height)
    {
        for (var slot = 0; ; slot++)
        {
            var x = GridOrigin + slot % GridColumns * GridX;
            var y = GridOrigin + slot / GridColumns * GridY;
            var overlaps = diagram.Shapes.Any(s => x < s.X + s.Width && s.X < x + width && y < s.Y + s.Height && s.Y < y + height);
            if (!overlaps)
            {
                return (x, y);
            }
        }
    }

    private static string NextId(Diagram diagram, string prefix) =>
        NextId(diagram.Shapes.Select(s => s.Id).Concat(diagram.Edges.Select(e => e.Id)).ToHashSet(), prefix);

    private static string NextId(HashSet<string> used, string prefix)
    {
        for (var n = 1; ; n++)
        {
            var id = prefix + n;
            if (used.Add(id))
            {
                return id;
            }
        }
    }

    private static string NewDiagramId() => Guid.NewGuid().ToString("N");

    private static string ValidName(string name) => ValidText(name, "Name");

    private static string? NonBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string ValidLabel(string label) => ValidText(label, "Label");

    private static string? ValidEdgeLabel(string? label) => string.IsNullOrWhiteSpace(label) ? null : ValidText(label, "Label");

    private static string ValidText(string value, string field)
    {
        var text = string.Join(' ', value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (text.Length == 0)
        {
            throw new DiagramValidationException($"{field} must not be empty.");
        }
        if (text.Length > MaxTextLength)
        {
            throw new DiagramValidationException($"{field} must not exceed {MaxTextLength} characters.");
        }
        return text;
    }

    private static double ValidSize(double value, string field) =>
        value is >= MinSize and <= MaxCoordinate ? value : throw new DiagramValidationException($"{field} must be between {MinSize} and {MaxCoordinate}.");

    private static double ValidCoordinate(double value, string field) =>
        value is >= 0 and <= MaxCoordinate ? value : throw new DiagramValidationException($"{field} must be between 0 and {MaxCoordinate}.");
}
