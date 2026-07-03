using System.Text.Json;
using MathLearning.Api.Endpoints;

namespace MathLearning.Tests.Endpoints;

public sealed class OperationIdentityResolutionTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"operationId\":\"   \"}")]
    [InlineData("{\"idempotencyKey\":\"   \"}")]
    [InlineData("{\"operationId\":\"   \",\"idempotencyKey\":\"   \"}")]
    public void QuizAnswerKeys_MissingOrWhitespace_ReturnsFalse(string json)
    {
        using var document = JsonDocument.Parse(json);

        var resolved = QuizEndpointHelpers.TryResolveQuizAnswerKeys(
            document.RootElement,
            out var operationId,
            out var idempotencyKey);

        Assert.False(resolved);
        Assert.Empty(operationId);
        Assert.Empty(idempotencyKey);
    }

    [Fact]
    public void QuizAnswerKeys_OnlyOperationId_UsesItForBothLedgerDimensions()
    {
        using var document = JsonDocument.Parse("""
            {"operationId":"  quiz-op-1  "}
            """);

        var resolved = QuizEndpointHelpers.TryResolveQuizAnswerKeys(
            document.RootElement,
            out var operationId,
            out var idempotencyKey);

        Assert.True(resolved);
        Assert.Equal("quiz-op-1", operationId);
        Assert.Equal("quiz-op-1", idempotencyKey);
    }

    [Fact]
    public void QuizAnswerKeys_OnlyIdempotencyKey_UsesItForBothLedgerDimensions()
    {
        using var document = JsonDocument.Parse("""
            {"idempotencyKey":"  quiz-key-1  "}
            """);

        var resolved = QuizEndpointHelpers.TryResolveQuizAnswerKeys(
            document.RootElement,
            out var operationId,
            out var idempotencyKey);

        Assert.True(resolved);
        Assert.Equal("quiz-key-1", operationId);
        Assert.Equal("quiz-key-1", idempotencyKey);
    }

    [Fact]
    public void QuizAnswerKeys_BothProvided_PreservesDistinctTrimmedValues()
    {
        using var document = JsonDocument.Parse("""
            {"operationId":"  quiz-op-2  ","idempotencyKey":"  quiz-key-2  "}
            """);

        var resolved = QuizEndpointHelpers.TryResolveQuizAnswerKeys(
            document.RootElement,
            out var operationId,
            out var idempotencyKey);

        Assert.True(resolved);
        Assert.Equal("quiz-op-2", operationId);
        Assert.Equal("quiz-key-2", idempotencyKey);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData(null, "   ")]
    [InlineData("   ", "   ")]
    public void SrsUpdateKeys_MissingOrWhitespace_ReturnsFalse(
        string? rawOperationId,
        string? rawIdempotencyKey)
    {
        var resolved = SrsEndpointHelpers.TryResolveSrsUpdateKeys(
            rawOperationId,
            rawIdempotencyKey,
            out var operationId,
            out var idempotencyKey);

        Assert.False(resolved);
        Assert.Empty(operationId);
        Assert.Empty(idempotencyKey);
    }

    [Fact]
    public void SrsUpdateKeys_OnlyOperationId_UsesItForBothLedgerDimensions()
    {
        var resolved = SrsEndpointHelpers.TryResolveSrsUpdateKeys(
            "  srs-op-1  ",
            null,
            out var operationId,
            out var idempotencyKey);

        Assert.True(resolved);
        Assert.Equal("srs-op-1", operationId);
        Assert.Equal("srs-op-1", idempotencyKey);
    }

    [Fact]
    public void SrsUpdateKeys_OnlyIdempotencyKey_UsesItForBothLedgerDimensions()
    {
        var resolved = SrsEndpointHelpers.TryResolveSrsUpdateKeys(
            null,
            "  srs-key-1  ",
            out var operationId,
            out var idempotencyKey);

        Assert.True(resolved);
        Assert.Equal("srs-key-1", operationId);
        Assert.Equal("srs-key-1", idempotencyKey);
    }

    [Fact]
    public void SrsUpdateKeys_BothProvided_PreservesDistinctTrimmedValues()
    {
        var resolved = SrsEndpointHelpers.TryResolveSrsUpdateKeys(
            "  srs-op-2  ",
            "  srs-key-2  ",
            out var operationId,
            out var idempotencyKey);

        Assert.True(resolved);
        Assert.Equal("srs-op-2", operationId);
        Assert.Equal("srs-key-2", idempotencyKey);
    }
}
