using Kandinsky.Server.Model;

namespace Kandinsky.Server.Diagrams;

/// <summary>Validates and normalizes client input; invalid input raises a <see cref="DiagramValidationException"/>.</summary>
public static class DiagramInput
{
    private const double MinSize = 20;
    private const double MaxCoordinate = 100_000;
    private const int MaxTextLength = 200;
    private const int MaxImportLength = 1_000_000;

    public static string ValidName(string name) => ValidText(name, "Name");

    public static string ValidLabel(string label) => ValidText(label, "Label");

    /// <summary>Edge labels are optional: a blank label means no label.</summary>
    public static string? ValidEdgeLabel(string? label) => string.IsNullOrWhiteSpace(label) ? null : ValidText(label, "Label");

    public static double ValidSize(double value, string field) =>
        value is >= MinSize and <= MaxCoordinate ? value : throw new DiagramValidationException($"{field} must be between {MinSize} and {MaxCoordinate}.");

    public static double ValidCoordinate(double value, string field) =>
        value is >= 0 and <= MaxCoordinate ? value : throw new DiagramValidationException($"{field} must be between 0 and {MaxCoordinate}.");

    public static string ValidImportText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new DiagramValidationException("Import text must not be empty.");
        }
        if (text.Length > MaxImportLength)
        {
            throw new DiagramValidationException($"Import text must not exceed {MaxImportLength} characters.");
        }
        return text;
    }

    public static void ValidateEndpoints(Diagram diagram, string? sourceId, string? targetId)
    {
        if (string.IsNullOrEmpty(sourceId) || string.IsNullOrEmpty(targetId))
        {
            throw new DiagramValidationException("Edge source and target are required.");
        }
        if (sourceId == targetId)
        {
            throw new DiagramValidationException("Edge source and target must be different shapes.");
        }
        EnsureShapesExist(diagram, [sourceId, targetId]);
    }

    public static void EnsureShapesExist(Diagram diagram, IEnumerable<string> shapeIds)
    {
        var missing = shapeIds.FirstOrDefault(id => diagram.Shapes.All(s => s.Id != id));
        if (missing is not null)
        {
            throw new DiagramValidationException($"Shape '{missing}' does not exist in this diagram.");
        }
    }

    // Names and labels are single-line: line breaks become spaces.
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
}
