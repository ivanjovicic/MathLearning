using System.Net;
using System.Net.Http.Json;
using MathLearning.Api;
using MathLearning.Application.DTOs.Common;
using MathLearning.Domain.Entities;
using MathLearning.Tests.Helpers;

namespace MathLearning.Tests.Endpoints;

public sealed class MobileCompatibilityEndpointsIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public MobileCompatibilityEndpointsIntegrationTests(CustomWebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UserProfileById_CompatibilityAlias_MatchesCanonicalRoute()
    {
        var canonical = await _client.GetAsync("/api/user/profile/1");
        var alias = await _client.GetAsync("/api/users/1/profile");

        Assert.Equal(canonical.StatusCode, alias.StatusCode);
        Assert.Equal(HttpStatusCode.OK, alias.StatusCode);

        var canonicalBody = await canonical.Content.ReadAsStringAsync();
        var aliasBody = await alias.Content.ReadAsStringAsync();
        Assert.Equal(canonicalBody, aliasBody);
    }

    [Fact]
    public async Task AdaptiveReview_CompatibilityAlias_MatchesCanonicalRoute()
    {
        var canonical = await _client.GetFromJsonAsync<ApiResult<List<ReviewItem>>>("/api/adaptive/reviews/due");
        var alias = await _client.GetFromJsonAsync<ApiResult<List<ReviewItem>>>("/api/adaptive/review");

        Assert.NotNull(canonical);
        Assert.NotNull(alias);
        Assert.Equal(canonical!.Success, alias!.Success);
        Assert.Equal(canonical.ErrorCode, alias.ErrorCode);
        Assert.Equal(canonical.Error, alias.Error);
        Assert.Equal(canonical.Data?.Count ?? 0, alias.Data?.Count ?? 0);
    }

    [Fact]
    public async Task AdaptiveSessionAnswer_WhenAdaptiveSessionItemIdMissing_ReturnsValidationBadRequest()
    {
        var payload = new
        {
            sessionId = Guid.NewGuid(),
            questionId = 1,
            answer = "42",
            responseTimeMs = 2500
        };

        var response = await _client.PostAsJsonAsync("/api/adaptive/session/answer", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Contains("AdaptiveSessionItemId", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AvatarMe_LegacyReadRoute_RemainsDistinctFromCanonicalMobileShape()
    {
        var canonical = await _client.GetAsync("/api/cosmetics/avatar");
        var alias = await _client.GetAsync("/api/avatar/me");

        Assert.Equal(HttpStatusCode.OK, canonical.StatusCode);
        Assert.Equal(HttpStatusCode.OK, alias.StatusCode);

        var canonicalBody = await canonical.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var aliasBody = await alias.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        Assert.True(canonicalBody.TryGetProperty("slots", out _));
        Assert.True(aliasBody.TryGetProperty("equipped", out _));
    }

    [Fact]
    public async Task PublicAppearance_CompatibilityAlias_MatchesCanonicalRoute()
    {
        var canonical = await _client.GetAsync("/api/cosmetics/avatar/1");
        var alias = await _client.GetAsync("/api/profile/1/appearance");

        Assert.Equal(canonical.StatusCode, alias.StatusCode);
        Assert.Equal(HttpStatusCode.OK, alias.StatusCode);

        var canonicalBody = await canonical.Content.ReadAsStringAsync();
        var aliasBody = await alias.Content.ReadAsStringAsync();
        Assert.Equal(canonicalBody, aliasBody);
    }
}
