namespace NtBot.Shared.Hosting;

/// <summary>
/// Hosts públicos no Coolify, no mesmo padrão BarberAI (<c>*.thinkautomated.com.br</c>).
/// </summary>
public static class CoolifyPublicHosts
{
    public const string WebHost = "mfdash.thinkautomated.com.br";
    public const string WebUrl = "https://mfdash.thinkautomated.com.br";
    public const string BarberAiHost = "barberai.thinkautomated.com.br";

    public static readonly string[] CorsOrigins =
    [
        WebUrl,
        "http://mfdash.thinkautomated.com.br",
        "http://localhost:3000",
        "http://localhost:3001",
        "http://localhost:5173",
        "http://localhost:5001",
        "https://localhost:5001",
        "http://hnoe3x858fi0ikuex9ubwr60.46.225.161.55.sslip.io",
        "https://hnoe3x858fi0ikuex9ubwr60.46.225.161.55.sslip.io"
    ];
}
