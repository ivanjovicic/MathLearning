using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using MathLearning.Api;
using MathLearning.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;

namespace MathLearning.Tests.Endpoints;

public sealed class MobileApiRouteContractTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public MobileApiRouteContractTests(CustomWebApplicationFactory<Program> factory)
    {
        _client = new AnonymousWebApplicationFactory().CreateClient();
    }

    [Theory]
    [InlineData("GET", "/api/quiz/questions")]
    [InlineData("POST", "/api/quiz/answer")]
    [InlineData("GET", "/api/progress/overview")]
    [InlineData("GET", "/api/progress/topics")]
    [InlineData("GET", "/api/adaptive/path")]
    [InlineData("GET", "/api/adaptive/reviews/due")]
    [InlineData("GET", "/api/adaptive/recommendations")]
    [InlineData("GET", "/api/leaderboard/rivals")]
    [InlineData("GET", "/api/users/profile")]
    [InlineData("GET", "/api/user/coins")]
    [InlineData("POST", "/api/daily-run/chest/claim")]
    public async Task MobileRoutes_Return401WithoutToken(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
        {
            request.Content = JsonContent.Create(new { });
        }

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/analytics/mastery")]
    [InlineData("/api/chase/test")]
    public async Task UnsupportedMobileRoutes_AreAbsent(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/adaptive/session/start")]
    [InlineData("/api/adaptive/session/answer")]
    public async Task UnsupportedMobileRoutes_ArePostOnly(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    private sealed class AnonymousWebApplicationFactory : CustomWebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication("Anonymous")
                    .AddScheme<AuthenticationSchemeOptions, NoAuthHandler>("Anonymous", _ => { });
            });
        }
    }

    private sealed class NoAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public NoAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }
    }
}
