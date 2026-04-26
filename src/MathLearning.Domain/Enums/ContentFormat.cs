using System.Text.Json.Serialization;

namespace MathLearning.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContentFormat
{
    PlainText = 0,
    LaTeX = 1,
    [Obsolete("Use LaTeX. This alias is kept for existing persisted values and callers.")]
    Latex = LaTeX,
    MarkdownWithMath = 2,
    Html = 3
}
