using MathLearning.Infrastructure.Services.Idempotency;

namespace MathLearning.Tests.Services;

public sealed class IdempotencyPayloadCanonicalizerTests
{
    [Fact]
    public void RequireValue_TrimsAndRejectsBlankInput()
    {
        Assert.Equal("hello", IdempotencyPayloadCanonicalizer.RequireValue("  hello  ", "value"));

        var ex = Assert.Throws<ArgumentException>(() => IdempotencyPayloadCanonicalizer.RequireValue("   ", "value"));
        Assert.Equal("value", ex.ParamName);
    }

    [Fact]
    public void CanonicalizeToJson_SortsPropertiesRecursively()
    {
        var payload = new
        {
            b = 2,
            a = 1,
            nested = new
            {
                z = 9,
                y = new[] { 2, 1 }
            }
        };

        var json = IdempotencyPayloadCanonicalizer.CanonicalizeToJson(payload);

        Assert.Equal("{\"a\":1,\"b\":2,\"nested\":{\"y\":[2,1],\"z\":9}}", json);
    }

    [Fact]
    public void ComputePayloadHash_UsesUppercaseSha256()
    {
        var hash = IdempotencyPayloadCanonicalizer.ComputePayloadHash("abc");

        Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", hash);
    }
}
