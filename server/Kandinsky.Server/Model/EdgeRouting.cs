using System.Text.Json.Serialization;

namespace Kandinsky.Server.Model;

/// <summary>How an edge is drawn: a straight line or an angled (orthogonal) path of horizontal and vertical segments.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EdgeRouting>))]
public enum EdgeRouting
{
    [JsonStringEnumMemberName("straight")]
    Straight,

    [JsonStringEnumMemberName("angled")]
    Angled,
}
