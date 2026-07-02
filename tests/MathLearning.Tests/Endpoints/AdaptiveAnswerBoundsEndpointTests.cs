using System.Net;
using System.Net.Http.Json;
using MathLearning.Api;
using MathLearning.Application.DTOs.Common;
using MathLearning.Tests.Helpers;

namespace MathLearning.Tests.Endpoints;

public sealed class AdaptiveAnswerBoundsEndpointTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AdaptiveAnswerBoundsEndpointTests(CustomWebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SessionAnswer_InvalidConfidence_ReturnsValidationError()
    {
        var payload = new
        {
            adaptiveSessionId = Guid.NewGuid(),
            adaptiveSessionItemId = Guid.NewGuid(),
            questionId = 1,
            answer = "42",
            confidence = 2
        };

        var response = await _client.PostAsJsonAsync("/api/adaptive/session/answer", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Contains("Confidence", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SessionAnswer_OversizedAnswer_ReturnsValidationError()
    {
        var payload = new
        {
            adaptiveSessionId = Guid.NewGuid(),
            adaptiveSessionItemId = Guid.NewGuid(),
            questionId = 1,
            answer = new string('x', 2001),
            confidence = 0.5
        };

        var response = await _client.PostAsJsonAsync("/api/adaptive/session/answer", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.NotNull(result);
        Assert.Equal("VALIDATION_ERROR", result!.ErrorCode);
        Assert.Contains("Answer", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SessionAnswer_InvalidConfidenceString_ReturnsValidationError()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/adaptive/session/answer")
        {
            Content = JsonContent.Create(new
            {
                adaptiveSessionId = Guid.NewGuid(),
                adaptiveSessionItemId = Guid.NewGuid(),
                questionId = 1,
                answer = "42",
                confidence = "not-a-number"
            })
        };

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.NotNull(result);
        Assert.Equal("VALIDATION_ERROR", result!.ErrorCode);
    }
}
