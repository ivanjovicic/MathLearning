namespace MathLearning.Admin.Services;

public static class ReturnUrlSanitizer
{
    public static string NormalizeLocalReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        if (!returnUrl.StartsWith('/'))
        {
            return "/";
        }

        if (!Uri.IsWellFormedUriString(returnUrl, UriKind.Relative))
        {
            return "/";
        }

        if (returnUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return "/";
        }

        return returnUrl;
    }
}
