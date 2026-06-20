namespace NtBot.Web.Services;

public static class ApiUrlResolver
{
    public static string Resolve(IConfiguration configuration, IHostEnvironment environment)
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("API_BASE_URL"),
            configuration["ApiSettings:BaseUrl"],
            environment.IsDevelopment() ? "http://localhost:5053" : null
        };

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            return candidate.Trim().TrimEnd('/');
        }

        throw new InvalidOperationException(
            "URL da API não configurada. Defina a variável de ambiente API_BASE_URL " +
            "(ou ApiSettings__BaseUrl) apontando para NtBot.Api.");
    }
}
