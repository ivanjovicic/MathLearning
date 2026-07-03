using System.Text.Json;
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
    public void CanonicalizeToJson_EquivalentObjectOrder_ProducesSameJsonAndHash()
    {
        var first = new Dictionary<string, object?>
        {
            ["sourceEvent"] = "daily_run",
            ["metadata"] = new Dictionary<string, object?>
            {
                ["seasonId"] = 7,
                ["day"] = "2026-07-03"
            }
        };
        var second = new Dictionary<string, object?>
        {
            ["metadata"] = new Dictionary<string, object?>
            {
                ["day"] = "2026-07-03",
                ["seasonId"] = 7
            },
            ["sourceEvent"] = "daily_run"
        };

        var firstJson = IdempotencyPayloadCanonicalizer.CanonicalizeToJson(first);
        var secondJson = IdempotencyPayloadCanonicalizer.CanonicalizeToJson(second);

        Assert.Equal(firstJson, secondJson);
        Assert.Equal(
            IdempotencyPayloadCanonicalizer.ComputePayloadHash(firstJson),
            IdempotencyPayloadCanonicalizer.ComputePayloadHash(secondJson));
    }

    [Fact]
    public void CanonicalizeToJson_PreservesArrayOrderBecauseArrayOrderIsPayloadSignificant()
    {
        var firstJson = IdempotencyPayloadCanonicalizer.CanonicalizeToJson(new { answers = new[] { 1, 2, 3 } });
        var reorderedJson = IdempotencyPayloadCanonicalizer.CanonicalizeToJson(new { answers = new[] { 3, 2, 1 } });

        Assert.Equal("{\"answers\":[1,2,3]}", firstJson);
        Assert.Equal("{\"answers\":[3,2,1]}", reorderedJson);
        Assert.NotEqual(firstJson, reorderedJson);
        Assert.NotEqual(
            IdempotencyPayloadCanonicalizer.ComputePayloadHash(firstJson),
            IdempotencyPayloadCanonicalizer.ComputePayloadHash(reorderedJson));
    }

    [Fact]
    public void CanonicalizeToJson_UsesWebNamingPolicyForClrObjects()
    {
        var json = IdempotencyPayloadCanonicalizer.CanonicalizeToJson(new
        {
            OperationId = "operation-1",
            SourceEvent = "daily_run"
        });

        Assert.Equal("{\"operationId\":\"operation-1\",\"sourceEvent\":\"daily_run\"}", json);
    }

    [Fact]
    public void CanonicalizeToJson_AcceptsJsonElementAndDetachesFromDocumentLifetime()
    {
        string canonical;
        using (var document = JsonDocument.Parse("""
            {"z":3,"nested":{"b":2,"a":1}}
            """))
        {
            canonical = IdempotencyPayloadCanonicalizer.CanonicalizeToJson(document.RootElement);
        }

        Assert.Equal("{\"nested\":{\"a\":1,\"b\":2},\"z\":3}", canonical);
    }

    [Fact]
    public void CanonicalizeToJson_NullAndPrimitiveValuesRemainStable()
    {
        Assert.Equal("null", IdempotencyPayloadCanonicalizer.CanonicalizeToJson(null));
        Assert.Equal("true", IdempotencyPayloadCanonicalizer.CanonicalizeToJson(true));
        Assert.Equal("42", IdempotencyPayloadCanonicalizer.CanonicalizeToJson(42));
        Assert.Equal("\"math\"", IdempotencyPayloadCanonicalizer.CanonicalizeToJson("math"));
    }

    [Fact]
    public void SerializePayload_ReturnsNullOnlyForMissingPayload()
    {
        Assert.Null(IdempotencyPayloadCanonicalizer.SerializePayload(null));
        Assert.Equal("{}", IdempotencyPayloadCanonicalizer.SerializePayload(new { }));
        Assert.Equal("null", IdempotencyPayloadCanonicalizer.CanonicalizeToJson(null));
    }

    [Fact]
    public void ComputePayloadHash_UsesUppercaseSha256()
    {
        var hash = IdempotencyPayloadCanonicalizer.ComputePayloadHash("abc");

        Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", hash);
    }

    [Fact]
    public void ComputePayloadHash_IsDeterministicAndSensitiveToPayloadChanges()
    {
        const string canonical = "{\"answer\":\"2\",\"questionId\":1}";
        const string changed = "{\"answer\":\"3\",\"questionId\":1}";

        var first = IdempotencyPayloadCanonicalizer.ComputePayloadHash(canonical);
        var second = IdempotencyPayloadCanonicalizer.ComputePayloadHash(canonical);
        var changedHash = IdempotencyPayloadCanonicalizer.ComputePayloadHash(changed);

        Assert.Equal(first, second);
        Assert.NotEqual(first, changedHash);
        Assert.Equal(64, first.Length);
        Assert.Matches("^[0-9A-F]{64}$", first);
    }
}
