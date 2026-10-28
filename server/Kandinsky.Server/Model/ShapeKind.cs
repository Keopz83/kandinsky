using System.Text.Json.Serialization;

namespace Kandinsky.Server.Model;

/// <summary>
/// How a shape is drawn: a plain rectangle (process), a database cylinder, a decision diamond, a group container,
/// or one of the other flowchart symbols.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ShapeKind>))]
public enum ShapeKind
{
    [JsonStringEnumMemberName("rectangle")]
    Rectangle,

    [JsonStringEnumMemberName("database")]
    Database,

    [JsonStringEnumMemberName("decision")]
    Decision,

    [JsonStringEnumMemberName("group")]
    Group,

    /// <summary>Start point: a small filled circle.</summary>
    [JsonStringEnumMemberName("start")]
    Start,

    /// <summary>End (stop) point: a framed circle.</summary>
    [JsonStringEnumMemberName("end")]
    End,

    /// <summary>Terminal point: a stadium (pill).</summary>
    [JsonStringEnumMemberName("terminal")]
    Terminal,

    /// <summary>Event: a rounded rectangle.</summary>
    [JsonStringEnumMemberName("rounded")]
    Rounded,

    /// <summary>Data input / output: a parallelogram.</summary>
    [JsonStringEnumMemberName("io")]
    InputOutput,

    /// <summary>Predefined process: a rectangle with double vertical sides.</summary>
    [JsonStringEnumMemberName("subroutine")]
    Subroutine,

    /// <summary>Document: a rectangle with a wavy bottom.</summary>
    [JsonStringEnumMemberName("document")]
    Document,

    /// <summary>Connector: a labeled circle.</summary>
    [JsonStringEnumMemberName("connector")]
    Connector,

    /// <summary>Preparation: a hexagon.</summary>
    [JsonStringEnumMemberName("preparation")]
    Preparation,

    /// <summary>Manual operation: a trapezoid, wider at the top.</summary>
    [JsonStringEnumMemberName("manual")]
    Manual,
}
