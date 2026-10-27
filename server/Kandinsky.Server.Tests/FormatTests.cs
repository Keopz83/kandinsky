using Kandinsky.Server.Diagrams;
using Kandinsky.Server.Formats;
using Kandinsky.Server.Model;

namespace Kandinsky.Server.Tests;

public class FormatTests
{
    private static readonly Diagram Shop = new(
        "d1",
        "Shop",
        [new Shape("s1", "Customer", 0, 0, 140, 60), new Shape("s2", "Order", 200, 0, 140, 60)],
        [new Edge("e1", "s1", "s2", "places")]);

    [Fact]
    public void Exports_plantuml_class_diagram()
    {
        var text = PlantUml.Export(Shop);

        Assert.Equal(
            "@startuml\ntitle Shop\nclass \"Customer\" as s1\nclass \"Order\" as s2\ns1 --> s2 : places\n@enduml\n",
            text);
    }

    [Fact]
    public void Exports_mermaid_class_diagram()
    {
        var text = Mermaid.Export(Shop);

        Assert.Equal(
            "---\ntitle: \"Shop\"\n---\nclassDiagram\n  class s1[\"Customer\"]\n  class s2[\"Order\"]\n  s1 --> s2 : places\n",
            text);
    }

    [Fact]
    public void Parses_plantuml_declarations_arrows_and_implicit_classes()
    {
        var parsed = PlantUml.Parse("""
            @startuml
            ' comment
            skinparam monochrome true
            title Zoo
            class Animal
            class "Big Cat" as Cat {}
            interface Pet
            Cat --> Animal : is a
            Pet <.. Dog
            Dog -> Keeper
            @enduml
            """);

        Assert.Equal("Zoo", parsed.Title);
        Assert.Equal(
            [new("Animal", "Animal"), new("Cat", "Big Cat"), new("Pet", "Pet"), new("Dog", "Dog"), new("Keeper", "Keeper")],
            parsed.Shapes);
        Assert.Equal(
            [new("Cat", "Animal", "is a"), new("Dog", "Pet", null), new("Dog", "Keeper", null)],
            parsed.Edges);
    }

    [Fact]
    public void Parses_mermaid_front_matter_labels_and_arrows()
    {
        var parsed = Mermaid.Parse("""
            ---
            title: Zoo
            ---
            classDiagram
              direction LR
              %% comment
              class Cat["Big Cat"]
              class Animal
              Cat --> Animal : is a
              Animal <.. Keeper
            """);

        Assert.Equal("Zoo", parsed.Title);
        Assert.Equal([new("Cat", "Big Cat"), new("Animal", "Animal"), new("Keeper", "Keeper")], parsed.Shapes);
        Assert.Equal([new("Cat", "Animal", "is a"), new("Keeper", "Animal", null)], parsed.Edges);
    }

    [Theory]
    [InlineData("@startuml\nclass A\nA -- B\n@enduml", "Line 3: unsupported syntax 'A -- B'.")]
    [InlineData("@startuml\nclass A {\n  +name : string\n}\n@enduml", "Line 2: class members are not supported yet.")]
    [InlineData("@startuml\nA --> A\n@enduml", "Line 2: self-referencing edge on 'A' is not supported.")]
    [InlineData("class A", "Line 1: expected @startuml.")]
    [InlineData("@startuml\nclass A", "Missing @enduml.")]
    public void Reports_invalid_plantuml(string text, string message)
    {
        var error = Assert.Throws<DiagramValidationException>(() => PlantUml.Parse(text));
        Assert.Equal(message, error.Message);
    }

    [Theory]
    [InlineData("flowchart LR\nA --> B", "Line 1: expected 'classDiagram'. Only Mermaid class diagrams are supported.")]
    [InlineData("classDiagram\nA <|-- B", "Line 2: unsupported syntax 'A <|-- B'.")]
    [InlineData("---\ntitle: X\nclassDiagram", "Unterminated front matter: missing closing '---'.")]
    public void Reports_invalid_mermaid(string text, string message)
    {
        var error = Assert.Throws<DiagramValidationException>(() => Mermaid.Parse(text));
        Assert.Equal(message, error.Message);
    }

    [Theory]
    [InlineData("@startuml\n@enduml", DiagramFormat.PlantUml)]
    [InlineData("\n' comment\n@startuml\n@enduml", DiagramFormat.PlantUml)]
    [InlineData("classDiagram", DiagramFormat.Mermaid)]
    [InlineData("---\ntitle: X\n---\nclassDiagram", DiagramFormat.Mermaid)]
    public void Detects_format(string text, DiagramFormat expected)
    {
        Assert.Equal(expected, DiagramFormats.Detect(text));
    }

    [Theory]
    [InlineData(DiagramFormat.PlantUml)]
    [InlineData(DiagramFormat.Mermaid)]
    public void Round_trips_through_export_and_parse(DiagramFormat format)
    {
        var parsed = DiagramFormats.Parse(DiagramFormats.Export(Shop, format), format);

        Assert.Equal("Shop", parsed.Title);
        Assert.Equal([new("s1", "Customer"), new("s2", "Order")], parsed.Shapes);
        Assert.Equal([new("s1", "s2", "places")], parsed.Edges);
    }
}
