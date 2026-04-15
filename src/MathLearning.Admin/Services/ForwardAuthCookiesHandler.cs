using Microsoft.AspNetCore.Http;

namespace MathLearning.Admin.Services;

public sealed class ForwardAuthCookiesHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor httpContextAccessor;

    public ForwardAuthCookiesHandler(IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var cookieHeader = httpContextAccessor.HttpContext?.Request.Headers.Cookie.ToString();
        if (!string.IsNullOrWhiteSpace(cookieHeader) && !request.Headers.Contains("Cookie"))
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
