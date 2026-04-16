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
        EnsureBaseAddress();
        return await httpClient.GetFromJsonAsync<T>(requestUri, cancellationToken);
    }

    public async Task<HttpResponseMessage> PostAsync(
        string requestUri,
        HttpContent? content = null,
        CancellationToken cancellationToken = default)
    {
        EnsureBaseAddress();
        return await httpClient.PostAsync(requestUri, content, cancellationToken);
    }

    public async Task<HttpResponseMessage> PostAsJsonAsync<T>(
        string requestUri,
        T value,
        CancellationToken cancellationToken = default)
    {
        EnsureBaseAddress();
        return await httpClient.PostAsJsonAsync(requestUri, value, cancellationToken);
    }

    public async Task<HttpResponseMessage> GetAsync(string requestUri, CancellationToken cancellationToken = default)
    {
        EnsureBaseAddress();
        return await httpClient.GetAsync(requestUri, cancellationToken);
    }

    private void EnsureBaseAddress()
    {
        if (httpClient.BaseAddress is not null)
        {
            return;
        }

        var apiBaseUrl = configuration["ApiBaseUrl"];
        if (!string.IsNullOrWhiteSpace(apiBaseUrl)
            && Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var baseUri))
        {
            httpClient.BaseAddress = baseUri;
            return;
        }

        logger.LogWarning("Admin API base address is not configured. Set ApiBaseUrl in configuration.");
    }
}
