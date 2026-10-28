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
            "@startuml\nclass \"Customer\" as s1\nclass \"Order\" as s2\ns1 --> s2 : places\n"
                + "' kandinsky:layout s1 x=0 y=0 width=140 height=60\n' kandinsky:layout s2 x=200 y=0 width=140 height=60\n@enduml\n",
            text);
    }

    [Fact]
    public void Exports_mermaid_class_diagram()
    {
        var text = Mermaid.Export(Shop);

        Assert.Equal(
            "classDiagram\n  class s1[\"Customer\"]\n  class s2[\"Order\"]\n  s1 --> s2 : places\n"
                + "  %% kandinsky:layout s1 x=0 y=0 width=140 height=60\n  %% kandinsky:layout s2 x=200 y=0 width=140 height=60\n",
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
    [InlineData("sequenceDiagram\nA->>B: hi", "Line 1: expected 'classDiagram' or 'flowchart'. Only Mermaid class diagrams and flowcharts are supported.")]
    [InlineData("classDiagram\nA <|-- B", "Line 2: unsupported syntax 'A <|-- B'.")]
    [InlineData("---\ntitle: X\nclassDiagram", "Unterminated front matter: missing closing '---'.")]
    [InlineData("flowchart LR\nA --- B", "Line 2: only directed links ('-->', '-.->', '==>') are supported.")]
    [InlineData("flowchart LR\nA <--> B", "Line 2: only directed links ('-->', '-.->', '==>') are supported.")]
    [InlineData("flowchart LR\nsubgraph one\nA --> B\nend", "Line 2: subgraphs are not supported yet.")]
    [InlineData("flowchart LR\nA[open --> B", "Line 2: unsupported syntax 'A[open --> B'.")]
    public void Reports_invalid_mermaid(string text, string message)
    {
        var error = Assert.Throws<DiagramValidationException>(() => Mermaid.Parse(text));
        Assert.Equal(message, error.Message);
    }

    [Fact]
    public void Parses_mermaid_flowchart_nodes_and_directed_links()
    {
        var parsed = Mermaid.Parse("""
            flowchart LR
                MXO[MXO REST API] --> DS[DataSource Backend<br/>CronJob]
                DS -->|gRPC bulk| ASSET[Asset.API]
                ASSET --> DB[(Oracle<br/>source of truth)]
                DB -- "via view" --> VIEW{{DB View}}:::view
                VIEW -.-> A & B ==> C(("Quoted (text)"));
                style A fill:#f9f
                classDef view stroke:#333
            """);

        Assert.Equal(
            [
                new("MXO", "MXO REST API"), new("DS", "DataSource Backend CronJob"), new("ASSET", "Asset.API"),
                new("DB", "Oracle source of truth", null, ShapeKind.Database), new("VIEW", "DB View"), new("A", "A"), new("B", "B"), new("C", "Quoted (text)"),
            ],
            parsed.Shapes);
        Assert.Equal(
            [
                new("MXO", "DS", null), new("DS", "ASSET", "gRPC bulk"), new("ASSET", "DB", null), new("DB", "VIEW", "via view"),
                new("VIEW", "A", null), new("VIEW", "B", null), new("A", "C", null), new("B", "C", null),
            ],
            parsed.Edges);
    }

    [Fact]
    public void Parses_mermaid_flowchart_databases()
    {
        var parsed = Mermaid.Parse("""
            flowchart LR
                ASSET[Asset.API] --> DB[(Oracle<br/>source of truth)]
                DB --> VIEW[DB View]
                KAFKA[Kafka Topic] -->|ES Sink Connector| ES[("Elasticsearch<br/>pra.env.index")]
                ES --> DB
            """);

        Assert.Equal(
            [
                new("ASSET", "Asset.API"), new("DB", "Oracle source of truth", null, ShapeKind.Database), new("VIEW", "DB View"),
                new("KAFKA", "Kafka Topic"), new("ES", "Elasticsearch pra.env.index", null, ShapeKind.Database),
            ],
            parsed.Shapes);
    }

    [Fact]
    public void Parses_plantuml_databases()
    {
        var parsed = PlantUml.Parse("""
            @startuml
            allowmixing
            class API
            database "Oracle DB" as DB
            database Cache
            API --> DB
            @enduml
            """);

        Assert.Equal(
            [new("API", "API"), new("DB", "Oracle DB", null, ShapeKind.Database), new("Cache", "Cache", null, ShapeKind.Database)],
            parsed.Shapes);
    }

    private static readonly Diagram Storage = new(
        "d2",
        "Storage",
        [new Shape("s1", "API", 0, 0, 140, 60), new Shape("s2", "Oracle \"main\"", 0, 120, 140, 80, ShapeKind.Database)],
        [new Edge("e1", "s1", "s2", "writes")]);

    [Fact]
    public void Exports_plantuml_databases()
    {
        Assert.Equal(
            "@startuml\nallowmixing\nclass \"API\" as s1\ndatabase \"Oracle 'main'\" as s2\ns1 --> s2 : writes\n"
                + "' kandinsky:layout s1 x=0 y=0 width=140 height=60\n' kandinsky:layout s2 x=0 y=120 width=140 height=80\n@enduml\n",
            PlantUml.Export(Storage));
    }

    [Fact]
    public void Exports_mermaid_flowchart_when_the_diagram_has_databases()
    {
        Assert.Equal(
            "flowchart TB\n  s1[\"API\"]\n  s2[(\"Oracle 'main'\")]\n  s1 -->|\"writes\"| s2\n"
                + "  %% kandinsky:layout s1 x=0 y=0 width=140 height=60\n  %% kandinsky:layout s2 x=0 y=120 width=140 height=80\n",
            Mermaid.Export(Storage));
    }

    [Theory]
    [InlineData(DiagramFormat.PlantUml)]
    [InlineData(DiagramFormat.Mermaid)]
    public void Round_trips_databases(DiagramFormat format)
    {
        var parsed = DiagramFormats.Parse(DiagramFormats.Export(Storage, format), format);

        Assert.Equal(
            [new("s1", "API", new(0, 0, 140, 60)), new("s2", "Oracle 'main'", new(0, 120, 140, 80), ShapeKind.Database)],
            parsed.Shapes);
        Assert.Equal([new("s1", "s2", "writes")], parsed.Edges);
    }

    [Theory]
    [InlineData("```mermaid\nclassDiagram\n```", "\nclassDiagram\n")]
    [InlineData("\n~~~~ plantuml\n@startuml\n@enduml\n~~~~\n", "\n\n@startuml\n@enduml\n\n")]
    [InlineData("classDiagram", "classDiagram")]
    public void Removes_markdown_code_fence(string text, string expected)
    {
        Assert.Equal(expected, DiagramFormats.Unfence(text));
    }

    [Fact]
    public void Reports_unclosed_code_fence()
    {
        var error = Assert.Throws<DiagramValidationException>(() => DiagramFormats.Unfence("```mermaid\nclassDiagram"));
        Assert.Equal("Line 1: code fence is not closed.", error.Message);
    }

    [Theory]
    [InlineData("@startuml\n@enduml", DiagramFormat.PlantUml)]
    [InlineData("\n' comment\n@startuml\n@enduml", DiagramFormat.PlantUml)]
    [InlineData("classDiagram", DiagramFormat.Mermaid)]
    [InlineData("---\ntitle: X\n---\nclassDiagram", DiagramFormat.Mermaid)]
    [InlineData("flowchart LR", DiagramFormat.Mermaid)]
    [InlineData("graph TD;", DiagramFormat.Mermaid)]
    [InlineData("sequenceDiagram", DiagramFormat.Mermaid)]
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

        Assert.Null(parsed.Title);
        Assert.Equal([new("s1", "Customer", new(0, 0, 140, 60)), new("s2", "Order", new(200, 0, 140, 60))], parsed.Shapes);
        Assert.Equal([new("s1", "s2", "places")], parsed.Edges);
    }

    [Fact]
    public void Parses_layout_comments_in_any_position()
    {
        var plantUml = PlantUml.Parse("""
            @startuml
            ' kandinsky:layout B x=10.5 y=20 width=100 height=50
            A --> B
            ' a normal comment
            ' kandinsky:layout A x=300 y=0 width=140 height=60
            @enduml
            """);
        var mermaid = Mermaid.Parse("""
            classDiagram
              A --> B
              %% kandinsky:layout A x=300 y=0 width=140 height=60
            """);

        Assert.Equal([new("A", "A", new(300, 0, 140, 60)), new("B", "B", new(10.5, 20, 100, 50))], plantUml.Shapes);
        Assert.Equal([new("A", "A", new(300, 0, 140, 60)), new("B", "B")], mermaid.Shapes);
    }

    [Theory]
    [InlineData("@startuml\nclass A\n' kandinsky:layout A x=1\n@enduml", "Line 3: invalid layout comment, expected 'kandinsky:layout <id> x=<x> y=<y> width=<width> height=<height>'.")]
    [InlineData("@startuml\nclass A\n' kandinsky:layout B x=1 y=2 width=3 height=4\n@enduml", "Line 3: layout for unknown shape 'B'.")]
    [InlineData("classDiagram\n%% kandinsky:layout A x=a y=2 width=3 height=4", "Line 2: invalid layout comment, expected 'kandinsky:layout <id> x=<x> y=<y> width=<width> height=<height>'.")]
    public void Reports_invalid_layout_comments(string text, string message)
    {
        var error = Assert.Throws<DiagramValidationException>(() => DiagramFormats.Parse(text, DiagramFormats.Detect(text)));
        Assert.Equal(message, error.Message);
    }
}
