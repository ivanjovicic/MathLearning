using Microsoft.AspNetCore.Http;

namespace MathLearning.Api.Services;

public sealed class RequestDataCacheService
{
    private readonly IHttpContextAccessor httpContextAccessor;

    public RequestDataCacheService(IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor;
    }

    public async Task<T?> GetOrAddAsync<T>(string key, Func<Task<T?>> factory)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return await factory();
        }

        if (httpContext.Items.TryGetValue(key, out var cached) && cached is T hit)
        {
            return hit;
        }

        var created = await factory();
        if (created is not null)
        {
            httpContext.Items[key] = created;
        }

        return created;
    }
}
