using MathLearning.Admin.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;

namespace MathLearning.Tests.Services;

public sealed class AdminApiClientTests
{
    [Fact]
    public async Task GetAsync_WithRelativeUri_UsesConfiguredBaseAddress()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5180")
        };
        var client = CreateClient(httpClient);

        var response = await client.GetAsync("/health");

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(new Uri("http://localhost:5180/health"), handler.RequestUri);
    }

    [Fact]
    public async Task GetAsync_WithRelativeUriAndMissingBaseAddress_ThrowsActionableError()
    {
        using var httpClient = new HttpClient(new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)));
        var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAsync("/health"));

        Assert.Contains("ApiBaseUrl", exception.Message, StringComparison.Ordinal);
        Assert.Contains("http://localhost:5180", exception.Message, StringComparison.Ordinal);
    }

    private static AdminApiClient CreateClient(HttpClient httpClient)
        => new(
            httpClient,
            new ConfigurationBuilder().Build(),
            NullLogger<AdminApiClient>.Instance);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage response;

        public RecordingHandler(HttpResponseMessage response)
        {
            this.response = response;
        }

        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(response);
        }
    }
}
