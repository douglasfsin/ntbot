namespace NtBot.Domain.Entities
{
    /// <summary>
    /// Usuário do sistema
    /// </summary>
    public class User
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Tenant? Tenant { get; set; }
        
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public bool EmailConfirmed { get; set; }
        
        // Role
        public UserRole Role { get; set; }
        
        // Status
        public bool IsActive { get; set; } = true;
        public DateTime? LastLogin { get; set; }
        
        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
    
    public enum UserRole
    {
        ADMIN,      // Administrador do tenant
        TRADER,     // Pode operar e ver tudo
        VIEWER      // Apenas visualização
    }
}
