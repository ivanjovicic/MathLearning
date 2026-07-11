using MathLearning.Api.Services;

namespace MathLearning.Tests.Services;

public sealed class LogOutputRedactorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Redact_NullOrEmpty_ReturnsEmpty(string? input)
    {
        Assert.Equal(string.Empty, LogOutputRedactor.Redact(input));
    }

    [Fact]
    public void Redact_SafeText_IsPreserved()
    {
        const string input = "Request completed with status 200 in 12 ms";

        var redacted = LogOutputRedactor.Redact(input);

        Assert.Equal(input, redacted);
    }

    [Fact]
    public void Redact_MasksEmailBearerAndSecretAssignments()
    {
        const string input = "user=leak@corp.test token=Bearer abc.def.ghi password=secret123";

        var redacted = LogOutputRedactor.Redact(input);

        Assert.DoesNotContain("leak@corp.test", redacted);
        Assert.DoesNotContain("Bearer abc.def.ghi", redacted);
        Assert.DoesNotContain("password=secret123", redacted);
        Assert.Contains("[redacted-email]", redacted);
        Assert.Contains("token=[redacted]", redacted);
        Assert.Contains("password=[redacted]", redacted);
    }

    [Theory]
    [InlineData("bearer TOKEN-VALUE")]
    [InlineData("BEARER another-token")]
    [InlineData("Bearer mixed.Case.Token")]
    public void Redact_BearerToken_IsCaseInsensitive(string input)
    {
        var redacted = LogOutputRedactor.Redact(input);

        Assert.Equal("[redacted-token]", redacted);
    }

    [Theory]
    [InlineData("pwd: hunter2", "hunter2")]
    [InlineData("secret = value", "value")]
    [InlineData("api-key=12345", "12345")]
    [InlineData("api_key:abcdef", "abcdef")]
    [InlineData("ConnectionString=Host=db", "Host=db")]
    [InlineData("TOKEN:raw-token", "raw-token")]
    public void Redact_SecretAssignmentVariants_RemoveValue(string input, string sensitiveValue)
    {
        var redacted = LogOutputRedactor.Redact(input);

        Assert.DoesNotContain(sensitiveValue, redacted, StringComparison.Ordinal);
        Assert.Contains("[redacted]", redacted);
    }

    [Fact]
    public void Redact_MasksMultipleSensitiveValuesInOneLine()
    {
        const string input =
            "alice@example.com called with Bearer token123 and api_key=top-secret from bob@example.org";

        var redacted = LogOutputRedactor.Redact(input);

        Assert.DoesNotContain("alice@example.com", redacted);
        Assert.DoesNotContain("bob@example.org", redacted);
        Assert.DoesNotContain("token123", redacted);
        Assert.DoesNotContain("top-secret", redacted);
        Assert.Equal(2, CountOccurrences(redacted, "[redacted-email]"));
    }

    [Fact]
    public void RedactLines_RedactsEveryLineAndPreservesOrder()
    {
        var lines = new[]
        {
            "first@example.com",
            "safe line",
            "Bearer hidden-token",
            "password=hidden-password"
        };

        var redacted = LogOutputRedactor.RedactLines(lines);

        Assert.Collection(
            redacted,
            line => Assert.Equal("[redacted-email]", line),
            line => Assert.Equal("safe line", line),
            line => Assert.Equal("[redacted-token]", line),
            line => Assert.Equal("password=[redacted]", line));
    }

    private static int CountOccurrences(string value, string token)
    {
        var count = 0;
        var index = 0;

        while ((index = value.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }
}
