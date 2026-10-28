using System.Text.Json.Serialization;

namespace Kandinsky.Server.Model;

/// <summary>
/// The corner of a decision diamond where an edge end is attached. Auto picks the corner facing the other end;
/// on other shapes the corner is ignored.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<EdgeCorner>))]
public enum EdgeCorner
{
    [JsonStringEnumMemberName("auto")]
    Auto,

    [JsonStringEnumMemberName("top")]
    Top,

    [JsonStringEnumMemberName("right")]
    Right,

    [JsonStringEnumMemberName("bottom")]
    Bottom,

    [JsonStringEnumMemberName("left")]
    Left,
}
