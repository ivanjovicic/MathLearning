using System.Net.Http.Json;

namespace MathLearning.Admin.Services;

public sealed class AdminApiClient
{
    private readonly HttpClient httpClient;
    private readonly IConfiguration configuration;
    private readonly ILogger<AdminApiClient> logger;

    public AdminApiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<AdminApiClient> logger)
    {
        this.httpClient = httpClient;
        this.configuration = configuration;
        this.logger = logger;
    }

    public async Task<T?> GetFromJsonAsync<T>(string requestUri, CancellationToken cancellationToken = default)
    {
        EnsureUsableRequestUri(requestUri);
        return await httpClient.GetFromJsonAsync<T>(requestUri, cancellationToken);
    }

    public async Task<HttpResponseMessage> PostAsync(
        string requestUri,
        HttpContent? content = null,
        CancellationToken cancellationToken = default)
    {
        EnsureUsableRequestUri(requestUri);
        return await httpClient.PostAsync(requestUri, content, cancellationToken);
    }

    public async Task<HttpResponseMessage> PostAsJsonAsync<T>(
        string requestUri,
        T value,
        CancellationToken cancellationToken = default)
    {
        EnsureUsableRequestUri(requestUri);
        return await httpClient.PostAsJsonAsync(requestUri, value, cancellationToken);
    }

    public async Task<HttpResponseMessage> GetAsync(string requestUri, CancellationToken cancellationToken = default)
    {
        EnsureUsableRequestUri(requestUri);
        return await httpClient.GetAsync(requestUri, cancellationToken);
    }

    private void EnsureUsableRequestUri(string requestUri)
    {
        if (Uri.TryCreate(requestUri, UriKind.Absolute, out _))
        {
            return;
        }

        if (httpClient.BaseAddress is not null)
        {
            return;
        }

        var configuredValue = configuration["ApiBaseUrl"];
        logger.LogError(
            "Admin API base address is not configured or invalid. ApiBaseUrl={ApiBaseUrl}",
            configuredValue);

        throw new InvalidOperationException(
            "Admin API base URL nije konfigurisan. Podesite ApiBaseUrl, npr. http://localhost:5180 za lokalni MathLearning.Api.");
    }
}
