using System.Text.Json;
using System.Text.RegularExpressions;
using Kandinsky.Server.Model;

namespace Kandinsky.Server.Diagrams;

/// <summary>Persists diagrams as one JSON file per diagram.</summary>
public sealed partial class DiagramFileStore(string directory)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public IEnumerable<Diagram> LoadAll()
    {
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
        {
            var diagram = JsonSerializer.Deserialize<Diagram>(File.ReadAllText(file), Options);
            if (diagram is not null && diagram.Id == Path.GetFileNameWithoutExtension(file))
            {
                yield return diagram;
            }
        }
    }

    public void Save(Diagram diagram)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(PathFor(diagram.Id), JsonSerializer.Serialize(diagram, Options));
    }

    public void Delete(string id) => File.Delete(PathFor(id));

    private string PathFor(string id) =>
        IdPattern().IsMatch(id)
            ? Path.Combine(directory, id + ".json")
            : throw new ArgumentException($"Invalid diagram id '{id}'.", nameof(id));

    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex IdPattern();
}
