using System.Net;
using MathLearning.Api;
using MathLearning.Application.Services;
using MathLearning.Infrastructure.Services.Idempotency;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Endpoints;

public sealed class IdempotencyObservabilityAuthorizationTests :
    IClassFixture<CustomWebApplicationFactory<Program>>
{
    private const string Path = "/api/monitoring/idempotency";

    private readonly CustomWebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public IdempotencyObservabilityAuthorizationTests(CustomWebApplicationFactory<Program> factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task ExplicitAnonymousCaller_IsDenied()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Path);
        request.Headers.Add(TestAuthHandler.AnonymousHeader, "true");

        var response = await client.SendAsync(request);

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401/403, got {(int)response.StatusCode}.");
    }

    [Fact]
    public async Task AuthenticatedLearner_IsForbidden()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Path);
        request.Headers.Add("X-Test-UserId", "observability-learner");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanReadSnapshotWithoutLeakingOperationOrUserIdentifiers()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var observability = scope.ServiceProvider.GetRequiredService<IdempotencyObservabilityService>();
            observability.Reset();
            observability.RecordReplay(
                "POST /api/quiz/answer",
                "quiz_answer",
                "private-operation-1234567890",
                "private-user@example.test",
                "completed");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, Path);
        request.Headers.Add("X-Test-UserId", "observability-admin");
        request.Headers.Add("X-Test-Roles", DesignTokenSecurity.AdminRole);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("private-operation-1234567890", body, StringComparison.Ordinal);
        Assert.DoesNotContain("private-user@example.test", body, StringComparison.Ordinal);
        Assert.Contains("quiz_answer", body, StringComparison.Ordinal);
        Assert.Contains("POST /api/quiz/answer", body, StringComparison.Ordinal);
    }

    [Fact]
    public void RouteMetadata_RequiresExactAdminPolicy()
    {
        _ = client;
        var dataSource = factory.Services.GetRequiredService<EndpointDataSource>();
        var endpoint = Assert.Single(dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Where(candidate => string.Equals(
                candidate.RoutePattern.RawText,
                Path,
                StringComparison.OrdinalIgnoreCase)));

        var authorization = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
        Assert.Contains(authorization, item =>
            string.Equals(item.Policy, DesignTokenSecurity.AdminPolicy, StringComparison.Ordinal));
    }
}
