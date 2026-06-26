using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MathLearning.Infrastructure.Services.Idempotency;

public static class IdempotencyPayloadCanonicalizer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string RequireValue(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{paramName} is required.", paramName);

        return value.Trim();
    }

    public static string ComputePayloadHash(string canonicalJson)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson));
        return Convert.ToHexString(bytes);
    }

    public static string? SerializePayload(object? payload)
    {
        return payload is null ? null : CanonicalizeToJson(payload);
    }

    public static string CanonicalizeToJson(object? payload)
    {
        var element = payload is JsonElement jsonElement
            ? jsonElement.Clone()
            : JsonSerializer.SerializeToElement(payload, JsonOptions);

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteCanonicalJson(element, writer);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteCanonicalJson(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalJson(property.Value, writer);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonicalJson(item, writer);
                }

                writer.WriteEndArray();
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }
}
