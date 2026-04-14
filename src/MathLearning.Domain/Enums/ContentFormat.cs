using System.Text.Json.Serialization;

namespace MathLearning.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContentFormat
{
    PlainText = 0,
    Latex = 1,
    MarkdownWithMath = 2,
    Html = 3
}
