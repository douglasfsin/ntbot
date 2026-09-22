namespace NtBot.Shared.Constants;

public static class NtBotRoles
{
    public const string Admin = "Admin";
    public const string Support = "Support";
    public const string Free = "Free";
    public const string Starter = "Starter";
    public const string Pro = "Pro";
    public const string Enterprise = "Enterprise";
    public const string Partner = "Partner";

    // Domain UserRole strings (JWT ClaimTypes.Role)
    public const string RoleAdmin = "ADMIN";
    public const string RoleTrader = "TRADER";
    public const string RoleViewer = "VIEWER";
    public const string RoleAdvisor = "ADVISOR";
    public const string RoleClient = "CLIENT";
}
