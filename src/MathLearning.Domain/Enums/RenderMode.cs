using System.Text.Json.Serialization;

namespace MathLearning.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RenderMode
{
    Auto = 0,
    Inline = 1,
    Display = 2
}
