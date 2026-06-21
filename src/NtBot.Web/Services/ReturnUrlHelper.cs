namespace NtBot.Web.Services;

public static class ReturnUrlHelper
{
    public static string Normalize(string? returnUrl, string fallback = "/app")
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return fallback;

        if (!returnUrl.StartsWith('/') || returnUrl.StartsWith("//", StringComparison.Ordinal))
            return fallback;

        return returnUrl;
    }
}
