using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;

namespace MathLearning.Admin.Services;

public sealed class AdminApiClient
{
    private readonly HttpClient httpClient;
    private readonly NavigationManager navigationManager;
    private readonly ILogger<AdminApiClient> logger;

    public AdminApiClient(
        HttpClient httpClient,
        NavigationManager navigationManager,
        ILogger<AdminApiClient> logger)
    {
        this.httpClient = httpClient;
        this.navigationManager = navigationManager;
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

        if (!Uri.TryCreate(navigationManager.BaseUri, UriKind.Absolute, out var baseUri))
        {
            logger.LogWarning("Unable to resolve admin API base address from NavigationManager.BaseUri: {BaseUri}", navigationManager.BaseUri);
            return;
        }

        httpClient.BaseAddress = baseUri;
    }
}
