namespace Kandinsky.Server.Model;

/// <summary>The diagram after a paste and the ids of the added shapes and edges.</summary>
public sealed record PasteResult(Diagram Diagram, IReadOnlyList<string> ShapeIds, IReadOnlyList<string> EdgeIds);
