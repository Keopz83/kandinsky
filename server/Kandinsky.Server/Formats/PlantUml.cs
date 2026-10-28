using Kandinsky.Server.Model;

namespace Kandinsky.Server.Formats;

/// <summary>
/// PlantUML class diagram subset: classes (and similar elements), databases, decisions (hexagons), flowchart symbols
/// (as the closest deployment elements), groups (packages and other containers) and directed associations.
/// </summary>
public static class PlantUml
{
    public static string Export(Diagram diagram) => PlantUmlWriter.Write(diagram);

    public static ParsedDiagram Parse(string text) => PlantUmlParser.Parse(text);
}
