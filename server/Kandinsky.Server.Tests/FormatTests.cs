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
    [InlineData("@startuml\npackage P {\nclass A\n@enduml", "Line 2: group 'P' is not closed.")]
    [InlineData("@startuml\nclass A\n}\n@enduml", "Line 3: '}' without an open group.")]
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
    [InlineData("flowchart LR\nsubgraph one\nA --> B", "Line 2: group 'one' is not closed.")]
    [InlineData("flowchart LR\nA --> B\nend", "Line 3: 'end' without an open group.")]
    [InlineData("flowchart LR\nsubgraph one\nsubgraph one\nend\nend", "Line 3: group 'one' cannot be nested in itself.")]
    [InlineData("classDiagram\nnamespace Shapes {\nclass A", "Line 2: group 'Shapes' is not closed.")]
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
                new("DB", "Oracle source of truth", null, ShapeKind.Database), new("VIEW", "DB View", null, ShapeKind.Preparation), new("A", "A"), new("B", "B"),
                new("C", "Quoted (text)", null, ShapeKind.Connector),
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

    [Fact]
    public void Parses_mermaid_subgraphs_and_decisions()
    {
        var parsed = Mermaid.Parse("""
            flowchart TB
                Start --> Check{Valid?}
                subgraph backend [Back end]
                    direction LR
                    API --> DB[(Oracle)]
                    subgraph jobs["Jobs"]
                        Sync
                    end
                end
                subgraph Front end
                    Check -->|yes| UI
                end
                Check -->|no| backend
                Start --> API
            """);

        Assert.Equal(
            [
                new("Start", "Start"), new("Check", "Valid?", null, ShapeKind.Decision, "group1"),
                new("backend", "Back end", null, ShapeKind.Group), new("API", "API", null, ShapeKind.Rectangle, "backend"),
                new("DB", "Oracle", null, ShapeKind.Database, "backend"), new("jobs", "Jobs", null, ShapeKind.Group, "backend"),
                new("Sync", "Sync", null, ShapeKind.Rectangle, "jobs"), new("group1", "Front end", null, ShapeKind.Group),
                new("UI", "UI", null, ShapeKind.Rectangle, "group1"),
            ],
            parsed.Shapes);
        Assert.Equal(
            [new("Start", "Check", null), new("API", "DB", null), new("Check", "UI", "yes"), new("Check", "backend", "no"), new("Start", "API", null)],
            parsed.Edges);
    }

    [Fact]
    public void Parses_mermaid_class_diagram_namespaces()
    {
        var parsed = Mermaid.Parse("""
            classDiagram
              namespace Shapes {
                class Square
              }
              Square --> Canvas
            """);

        Assert.Equal(
            [new("Shapes", "Shapes", null, ShapeKind.Group), new("Square", "Square", null, ShapeKind.Rectangle, "Shapes"), new("Canvas", "Canvas")],
            parsed.Shapes);
    }

    [Fact]
    public void Parses_plantuml_containers_and_decisions()
    {
        var parsed = PlantUml.Parse("""
            @startuml
            allowmixing
            package "Back end" as backend {
              class API
              rectangle Jobs {
                database Cache
              }
            }
            package com.example.ui {
            }
            hexagon "Valid?" as Check
            diamond D2
            <> D3
            Check --> backend : no
            @enduml
            """);

        Assert.Equal(
            [
                new("backend", "Back end", null, ShapeKind.Group), new("API", "API", null, ShapeKind.Rectangle, "backend"),
                new("Jobs", "Jobs", null, ShapeKind.Group, "backend"), new("Cache", "Cache", null, ShapeKind.Database, "Jobs"),
                new("group1", "com.example.ui", null, ShapeKind.Group), new("Check", "Valid?", null, ShapeKind.Decision),
                new("D2", "D2", null, ShapeKind.Decision), new("D3", "D3", null, ShapeKind.Decision),
            ],
            parsed.Shapes);
        Assert.Equal([new("Check", "backend", "no")], parsed.Edges);
    }

    private static readonly Diagram Flow = new(
        "d3",
        "Flow",
        [
            new Shape("s1", "Valid?", 0, 0, 140, 80, ShapeKind.Decision),
            new Shape("g1", "Back end", 200, 0, 400, 300, ShapeKind.Group),
            new Shape("s2", "API", 220, 40, 140, 60),
            new Shape("g2", "Jobs", 220, 120, 200, 160, ShapeKind.Group),
            new Shape("s3", "Cache", 240, 160, 140, 60, ShapeKind.Database),
        ],
        [new Edge("e1", "s1", "s2", "yes"), new Edge("e2", "s1", "g1", null)]);

    [Fact]
    public void Exports_plantuml_packages_and_hexagons()
    {
        Assert.Equal(
            """
            @startuml
            allowmixing
            hexagon "Valid?" as s1
            package "Back end" as g1 {
              class "API" as s2
              package "Jobs" as g2 {
                database "Cache" as s3
              }
            }
            s1 --> s2 : yes
            s1 --> g1

            """.Replace("\r", "") + string.Concat(Flow.Shapes.Select(s => $"' kandinsky:layout {s.Id} x={s.X} y={s.Y} width={s.Width} height={s.Height}\n")) + "@enduml\n",
            PlantUml.Export(Flow));
    }

    [Fact]
    public void Exports_mermaid_flowchart_with_subgraphs_and_decisions()
    {
        Assert.Equal(
            """
            flowchart TB
              s1{"Valid?"}
              subgraph g1 ["Back end"]
                s2["API"]
                subgraph g2 ["Jobs"]
                  s3[("Cache")]
                end
              end
              s1 -->|"yes"| s2
              s1 --> g1

            """.Replace("\r", "") + string.Concat(Flow.Shapes.Select(s => $"  %% kandinsky:layout {s.Id} x={s.X} y={s.Y} width={s.Width} height={s.Height}\n")),
            Mermaid.Export(Flow));
    }

    [Theory]
    [InlineData(DiagramFormat.PlantUml)]
    [InlineData(DiagramFormat.Mermaid)]
    public void Round_trips_groups_and_decisions(DiagramFormat format)
    {
        var parsed = DiagramFormats.Parse(DiagramFormats.Export(Flow, format), format);

        Assert.Equal(
            Flow.Shapes.Select(s => (s.Id, s.Label, s.Kind, s.X, s.Y, s.Width, s.Height)),
            parsed.Shapes.Select(s => (s.Id, s.Label, s.Kind, s.Bounds!.X, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height)));
        Assert.Equal([null, "g1", "g1", "g2"], parsed.Shapes.Where(s => s.Id != "s1").Select(s => s.ParentId));
        Assert.Equal([new("s1", "s2", "yes"), new("s1", "g1", null)], parsed.Edges);
    }

    [Fact]
    public void Parses_mermaid_flowchart_symbols_in_classic_and_v11_syntax()
    {
        var parsed = Mermaid.Parse("""
            flowchart TB
                S@{ shape: sm-circ, label: "Begin" } --> T([Terminal])
                T --> R(Event) --> IO[/Read input/] --> IO2[\Write output\]
                IO2 --> SUB[[Validate]] --> P{{Prepare}} --> M[\Approve/]
                M --> D@{ shape: doc, label: "Report" } --> C((A)) --> E(((Done)))
                E --> E2@{ shape: fr-circ } & E3@{ shape: stop, label: Stop } & O[/Priority\] & X@{ shape: bolt }
            """);

        Assert.Equal(
            [
                ("S", "Begin", ShapeKind.Start), ("T", "Terminal", ShapeKind.Terminal), ("R", "Event", ShapeKind.Rounded),
                ("IO", "Read input", ShapeKind.InputOutput), ("IO2", "Write output", ShapeKind.InputOutput),
                ("SUB", "Validate", ShapeKind.Subroutine), ("P", "Prepare", ShapeKind.Preparation), ("M", "Approve", ShapeKind.Manual),
                ("D", "Report", ShapeKind.Document), ("C", "A", ShapeKind.Connector), ("E", "Done", ShapeKind.End),
                ("E2", "E2", ShapeKind.End), ("E3", "Stop", ShapeKind.End), ("O", "Priority", ShapeKind.Rectangle), ("X", "X", ShapeKind.Rectangle),
            ],
            parsed.Shapes.Select(s => (s.Id, s.Label, s.Kind)));
    }

    private static readonly ShapeKind[] FlowchartKinds =
    [
        ShapeKind.Start, ShapeKind.End, ShapeKind.Terminal, ShapeKind.Rounded, ShapeKind.InputOutput, ShapeKind.Subroutine,
        ShapeKind.Document, ShapeKind.Connector, ShapeKind.Preparation, ShapeKind.Manual, ShapeKind.Decision, ShapeKind.Database,
    ];

    private static readonly Diagram Symbols = new(
        "d4",
        "Symbols",
        FlowchartKinds.Select((k, i) => new Shape($"s{i}", k.ToString(), i * 200, 0, 140, 60, k)).ToList(),
        [new Edge("e1", "s0", "s1", "go", EdgeRouting.Angled), new Edge("e2", "s1", "s2", null), new Edge("e3", "s0", "s1", null, EdgeRouting.Angled)]);

    [Fact]
    public void Exports_mermaid_flowchart_symbols_and_angled_edges()
    {
        var text = Mermaid.Export(Symbols);

        Assert.StartsWith(
            """
            flowchart TB
              s0@{ shape: sm-circ, label: "Start" }
              s1@{ shape: fr-circ, label: "End" }
              s2(["Terminal"])
              s3("Rounded")
              s4[/"InputOutput"/]
              s5[["Subroutine"]]
              s6@{ shape: doc, label: "Document" }
              s7(("Connector"))
              s8{{"Preparation"}}
              s9[\"Manual"/]
              s10{"Decision"}
              s11[("Database")]
              s0 -->|"go"| s1
              s1 --> s2
              s0 --> s1

            """.Replace("\r", ""),
            text);
        Assert.EndsWith("  %% kandinsky:edge s0 s1 routing=angled\n  %% kandinsky:edge s0 s1 routing=angled\n", text);
    }

    [Fact]
    public void Exports_plantuml_flowchart_symbols_with_the_kind_in_the_layout_comment_when_approximated()
    {
        var text = PlantUml.Export(Symbols);

        Assert.StartsWith(
            """
            @startuml
            allowmixing
            circle "Start" as s0
            circle "End" as s1
            usecase "Terminal" as s2
            storage "Rounded" as s3
            rectangle "InputOutput" as s4
            rectangle "Subroutine" as s5
            file "Document" as s6
            circle "Connector" as s7
            hexagon "Preparation" as s8
            rectangle "Manual" as s9
            hexagon "Decision" as s10
            database "Database" as s11

            """.Replace("\r", ""),
            text);
        Assert.Contains("' kandinsky:layout s0 x=0 y=0 width=140 height=60\n", text);
        Assert.Contains("' kandinsky:layout s1 x=200 y=0 width=140 height=60 kind=end\n", text);
        Assert.Contains("' kandinsky:layout s8 x=1600 y=0 width=140 height=60 kind=preparation\n", text);
        Assert.Contains("' kandinsky:edge s0 s1 routing=angled\n", text);
        Assert.DoesNotContain("linetype", text);
    }

    [Theory]
    [InlineData(DiagramFormat.PlantUml)]
    [InlineData(DiagramFormat.Mermaid)]
    public void Round_trips_flowchart_symbols_and_edge_routing(DiagramFormat format)
    {
        var parsed = DiagramFormats.Parse(DiagramFormats.Export(Symbols, format), format);

        Assert.Equal(Symbols.Shapes.Select(s => (s.Id, s.Label, s.Kind)), parsed.Shapes.Select(s => (s.Id, s.Label, s.Kind)));
        Assert.Equal(
            [new("s0", "s1", "go", EdgeRouting.Angled), new("s1", "s2", null), new("s0", "s1", null, EdgeRouting.Angled)],
            parsed.Edges);
    }

    [Theory]
    [InlineData(DiagramFormat.PlantUml, "skinparam linetype ortho\n")]
    [InlineData(DiagramFormat.Mermaid, "%%{init: {\"flowchart\": {\"curve\": \"step\"}}}%%\n")]
    public void Exports_and_reads_all_angled_edges_as_the_format_setting(DiagramFormat format, string setting)
    {
        var diagram = Shop with { Edges = [.. Shop.Edges.Select(e => e with { Routing = EdgeRouting.Angled })] };

        var text = DiagramFormats.Export(diagram, format);
        var parsed = DiagramFormats.Parse(text, format);

        Assert.Contains(setting, text);
        Assert.DoesNotContain("kandinsky:edge", text);
        Assert.Equal([new("s1", "s2", "places", EdgeRouting.Angled)], parsed.Edges);
    }

    [Fact]
    public void Reads_step_curves_from_mermaid_front_matter()
    {
        var parsed = Mermaid.Parse("---\nconfig:\n  flowchart:\n    curve: stepAfter\n---\nflowchart LR\nA --> B");

        Assert.Equal([new("A", "B", null, EdgeRouting.Angled)], parsed.Edges);
    }

    [Theory]
    [InlineData(DiagramFormat.PlantUml, "' kandinsky:edge s1 s2 routing=straight from=bottom to=left\n")]
    [InlineData(DiagramFormat.Mermaid, "  %% kandinsky:edge s1 s2 routing=straight from=bottom to=left\n")]
    public void Exports_and_reads_fixed_diamond_corners_in_the_edge_comment(DiagramFormat format, string comment)
    {
        var diagram = Shop with
        {
            Shapes = [.. Shop.Shapes.Select(s => s.Id == "s1" ? s with { Kind = ShapeKind.Decision } : s)],
            Edges = [.. Shop.Edges.Select(e => e with { SourceCorner = EdgeCorner.Bottom, TargetCorner = EdgeCorner.Left })],
        };

        var text = DiagramFormats.Export(diagram, format);
        var parsed = DiagramFormats.Parse(text, format);

        Assert.Contains(comment, text);
        Assert.Equal([new("s1", "s2", "places", EdgeRouting.Straight, EdgeCorner.Bottom, EdgeCorner.Left)], parsed.Edges);
    }

    [Fact]
    public void Reads_a_single_fixed_corner()
    {
        var parsed = Mermaid.Parse("flowchart LR\nA{Ok?} --> B\n%% kandinsky:edge A B routing=angled from=right");

        Assert.Equal([new("A", "B", null, EdgeRouting.Angled, EdgeCorner.Right)], parsed.Edges);
    }

    [Theory]
    [InlineData("flowchart LR\nA --> B\n%% kandinsky:edge A C routing=angled", "Line 3: routing for unknown edge 'A' -> 'C'.")]
    [InlineData("flowchart LR\nA --> B\n%% kandinsky:edge A B routing=curvy", "Line 3: invalid edge comment, expected 'kandinsky:edge <source> <target> routing=straight|angled'.")]
    [InlineData("flowchart LR\nA --> B\n%% kandinsky:edge A B routing=angled from=middle", "Line 3: unknown edge corner 'middle', expected top, right, bottom, left or auto.")]
    [InlineData("flowchart LR\nA --> B\n%% kandinsky:layout A x=1 y=2 width=30 height=40 kind=blob", "Line 3: unknown shape kind 'blob'.")]
    public void Reports_invalid_kandinsky_comments(string text, string message)
    {
        var error = Assert.Throws<DiagramValidationException>(() => Mermaid.Parse(text));
        Assert.Equal(message, error.Message);
    }

    [Fact]
    public void Generated_group_ids_avoid_ids_declared_later()
    {
        var parsed = Mermaid.Parse("flowchart TB\n  subgraph \"Backend\"\n    A\n  end\n  group1[Cache] --> A");

        Assert.Equal(
            [("group2", "Backend", ShapeKind.Group), ("A", "A", ShapeKind.Rectangle), ("group1", "Cache", ShapeKind.Rectangle)],
            parsed.Shapes.Select(s => (s.Id, s.Label, s.Kind)));
    }

    [Fact]
    public void Plantuml_keeps_a_class_declared_outside_a_package_outside_it()
    {
        var parsed = PlantUml.Parse("@startuml\nclass A\npackage P {\n  class B\n  A --> B\n}\n@enduml");

        Assert.Equal([("A", (string?)null), ("P", null), ("B", "P")], parsed.Shapes.Select(s => (s.Id, s.ParentId)));
    }
}
