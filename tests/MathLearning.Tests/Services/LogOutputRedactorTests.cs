using MathLearning.Api.Services;

namespace MathLearning.Tests.Services;

public sealed class LogOutputRedactorTests
{
    [Fact]
    public void Redact_MasksEmailBearerAndSecretAssignments()
    {
        const string input = "user=leak@corp.test token=Bearer abc.def.ghi password=secret123";
        var redacted = LogOutputRedactor.Redact(input);

        Assert.DoesNotContain("leak@corp.test", redacted);
        Assert.DoesNotContain("Bearer abc.def.ghi", redacted);
        Assert.DoesNotContain("password=secret123", redacted);
        Assert.Contains("[redacted-email]", redacted);
        Assert.Contains("[redacted-token]", redacted);
    }
}
